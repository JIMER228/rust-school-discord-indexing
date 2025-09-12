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
using Oxide.Core.Libraries;
using Newtonsoft.Json.Linq;
using Network;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("ReportSystem", "EcoSmile", "1.1.3")]
    class ReportSystem : RustPlugin
    {
        [PluginReference]
        Plugin ImageLibrary;
        static ReportSystem ins;
        PluginConfig config;

        public class PluginConfig
        {
            [JsonProperty("Время на добавления модератора в Дискорде (в cекундах)")]
            public float DsAddTime;
            [JsonProperty("Привилегия для доступа к меню модератора")]
            public string ModeratorPermis;
            [JsonProperty("Привилегия для доступа к меню Администратора")]
            public string AdminPermis;
            [JsonProperty("Команда используемая для бана (%STEAMID% - место вставки steamid игрока, %REASON% - место вставки причины, %TIME% - вместо вставки времени бана)")]
            public string BanCommand;
            [JsonProperty("Показывать кнопку репорта после смерти игрока?")]
            public bool DeathButton;
            [JsonProperty("CD на отправу репортов (в секундах)")]
            public float ReportCD;
            [JsonProperty("Список причина репорта игрока")]
            public List<string> Reasons;
            [JsonProperty("Список причина бана игрока")]
            public List<string> BanPressets;
            [JsonProperty("Список времени бана игрока")]
            public List<string> BanTimes;


            [JsonProperty("Настройка Vk")]
            public VKSettings VKSettings;
            [JsonProperty("Настройка Discord")]
            public DiscordSetting DiscordSetting;
            [JsonProperty("Настройка RCC")]
            public RCCSettings RCCSettings;
        }

        //public class UserRewards
        //{
        //    public bool EnableRewards;

        //}

        //public class PrizeSetting
        //{
        //    [JsonProperty("Шортнейм предмета")]
        //    public string ShortName;
        //    [JsonProperty("Исполняемая команда (%STEAMID% - ключ для вставки SteamID64 игрока)")]
        //    public string Command;
        //    [JsonProperty("Кастмоное имя предмета")]
        //    public string CustomName;
        //    [JsonProperty("Минимальное количество предмета")]
        //    public int MinAmount;
        //    [JsonProperty("Максимальное количество предмета")]
        //    public int MaxAmount;
        //    [JsonProperty("SkinID предмета")]
        //    public ulong SkinID;

        //    public PrizeSetting Get()
        //    {
        //        var prize = new PrizeSetting();
        //        prize.ShortName = ShortName;
        //        prize.Command = Command;
        //        prize.CustomName = CustomName;
        //        var amount = UnityEngine.Random.Range(MinAmount, MaxAmount);
        //        prize.MinAmount = amount;
        //        prize.MaxAmount = amount;
        //        prize.SkinID = SkinID;
        //        return prize;
        //    }
        //}

        public class RCCSettings
        {
            [JsonProperty("Включить поддержку RCC?")]
            public bool Enable;
            [JsonProperty("API key к RCC")]
            public string APIKey;
            [JsonProperty("Кикать игроков с указанными причинами бана?")]
            public bool KickPlayers;
            [JsonProperty("Список причин бана с которыми НЕ пускать игрока на сервер")]
            public List<string> BanReasons;
            [JsonProperty("Отправлять логи в ВК о срабатывании RCC?")]
            public bool VKLog;
            [JsonProperty("Отправлять логи в Discord о срабатывании RCC?")]
            public bool DSLog;
        }

        public class DiscordSetting
        {
            [JsonProperty("Включить поддержку Discord?")]
            public bool Enable;
            [JsonProperty("ВебХук канала для отправки репортов")]
            public string WebHook;
            [JsonProperty("ВебХук канала для отправки логов о проверке, бане и RCC")]
            public string LogWebHook;
        }

        public class VKSettings
        {
            [JsonProperty("Включить поддержку ВК?")]
            public bool Enable;
            [JsonProperty("ID чата для отправки Репортов (чтобы получить ID бесед группы, введите getvkid в консоль сервера)")]
            public string ChatID;
            [JsonProperty("ID чата для отправки Логов (Проверки, баны, срабатывания RCC, введите getvkid в консоль сервера)")]
            public string LogChatID;
            [JsonProperty("VK Token группы (для сообщений)")]
            public string AccesToken;
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                DsAddTime = 300.0f,
                ModeratorPermis = "reportsystem.moderatorpage",
                AdminPermis = "reportsystem.adminpage",
                BanCommand = "ban %STEAMID% %REASON%",
                DeathButton = false,
                ReportCD = 300,
                Reasons = new List<string>()
                {
                    "Wall Hack", "Aim assist", "No Recoil", "Fly Hack", "Macros", "Team Limit (2+)"
                },
                BanPressets = new List<string>()
                {
                    "Wall Hack", "Aim assist", "No Recoil", "Fly Hack", "Macros", "Team Limit (2+)"
                },
                BanTimes = new List<string>()
                {
                    "1d", "3d", "7d", "14d", "30d", "PERMANENT"
                },
                VKSettings = new VKSettings()
                {
                    AccesToken = "",
                    Enable = false,
                    ChatID = "1",
                    LogChatID = "1"
                },
                DiscordSetting = new DiscordSetting()
                {
                    Enable = false,
                    LogWebHook = "",
                    WebHook = "",
                },
                RCCSettings = new RCCSettings()
                {
                    Enable = false,
                    APIKey = "",
                    DSLog = true,
                    VKLog = false,
                    KickPlayers = true,
                    BanReasons = new List<string>()
                    {
                        "multiacc", "cheat", "macros", "чит", "хак", "макрос", "hack", "wh", "aim"
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

        public enum ReportStatys { Open, Process, Closed }
        public class Report
        {
            public ReportStatys status;
            public string VictimName;
            public string SenderName;
            public string ReportReson;
            public string ReportID;
            public DateTime ReportDate;
            public ulong SendreID;
        }

        public class UserReportData
        {
            public string UserName;
            public List<Report> Reports;
        }

        Dictionary<ulong, UserReportData> PlayerReports = new Dictionary<ulong, UserReportData>();

        public class ModeratorInfo
        {
            public string UserName = "";
            public int BanCount = 0;
            public int CheckCount = 0;
            public string DiscordID = "";
        }

        Dictionary<ulong, ModeratorInfo> moderatorData = new Dictionary<ulong, ModeratorInfo>();

        void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name + "/Reports"))
                PlayerReports = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, UserReportData>>(Name + "/Reports");
            else
                Interface.Oxide.DataFileSystem.WriteObject(Name + "/Reports", PlayerReports = new Dictionary<ulong, UserReportData>());

            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name + "/ModeratorBase"))
                moderatorData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, ModeratorInfo>>(Name + "/ModeratorBase");
            else
                Interface.Oxide.DataFileSystem.WriteObject(Name + "/ModeratorBase", moderatorData = new Dictionary<ulong, ModeratorInfo>());
        }

        private void OnServerInitialized()
        {
            ins = this;

            LoadData();
            permission.RegisterPermission(config.ModeratorPermis, this);
            PrintWarning($"Registered permission {config.ModeratorPermis}");
            permission.RegisterPermission(config.AdminPermis, this);
            PrintWarning($"Registered permission {config.AdminPermis}");

            //DrawMainUI(BasePlayer.activePlayerList.First());
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "MainReport.BG");
                CuiHelper.DestroyUi(player, "ReportAlert");
                CuiHelper.DestroyUi(player, "UI_Moderator");
                CuiHelper.DestroyUi(player, "DeatReport.BTN");
                UnityEngine.Object.Destroy(player.GetComponent<ReportPlayer>());
            }

            foreach (var report in PlayerReports)
            {
                foreach (var rp in report.Value.Reports)
                    if (rp.status == ReportStatys.Process)
                        rp.status = ReportStatys.Open;
            }

            Interface.Oxide.DataFileSystem.WriteObject(Name + "/ModeratorBase", moderatorData);
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/Reports", PlayerReports);
        }

        void OnServerSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/ModeratorBase", moderatorData);
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/Reports", PlayerReports);
        }

        void OnNewSave()
        {

        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            if (player.IsReceivingSnapshot)
            {
                timer.In(2f, () => OnPlayerConnected(player));
                return;
            }

            if (permission.UserHasPermission(player.UserIDString, config.ModeratorPermis))
            {
                if (moderatorData.ContainsKey(player.userID) && string.IsNullOrEmpty(moderatorData[player.userID].DiscordID))
                    SendReply(player, $"Чтобы вести работу с системой репортов, необходимо подключить свой Discrod.\nВведите /ds #ID, где #ID - ID в вашем дискорде.\nНапример: /ds EcoSmile#0001");
                else if (!moderatorData.ContainsKey(player.userID))
                    SendReply(player, $"Чтобы вести работу с системой репортов, необходимо подключить свой Discrod.\nВведите /ds #ID, где #ID - ID в вашем дискорде.\nНапример: /ds EcoSmile#0001");
            }

        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player.GetComponent<ReportPlayer>() != null)
            {
                player.GetComponent<ReportPlayer>().Disconect();
                UnityEngine.Object.Destroy(player.GetComponent<ReportPlayer>());
            }
        }

        [ConsoleCommand("getvkid")]
        void GetVkChatID(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            webrequest.Enqueue("https://api.vk.com/method/messages.getConversations?v=5.90" + $"&access_token={config.VKSettings.AccesToken}", "", (code, res) =>
            {
                var responce = JsonConvert.DeserializeObject<Rootobject>(res);
                var list = responce.response.items;
                string msg = "Список достпуных чатов Группы:\n";
                foreach (var a in list)
                {
                    if (a.conversation.peer.type == "chat")
                    {
                        msg += $"Chat Name: {a.conversation.chat_settings.title} [Chat ID: {a.conversation.peer.id - 2000000000}]\n";
                    }
                }
                PrintWarning(msg);

            }, this, RequestMethod.GET);
        }

        [ChatCommand("ds")]
        void AddDiscord(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, config.ModeratorPermis)) return;
            if (args.Length == 0)
            {
                SendReply(player, $"Введите /ds #ID, где #ID - ID в вашем дискорде.\nНапример: /ds EcoSmile#0001");
                return;
            }
            var id = args[0];
            if (moderatorData.ContainsKey(player.userID))
                moderatorData[player.userID].DiscordID = id;
            else
            {
                moderatorData[player.userID] = new ModeratorInfo();
                moderatorData[player.userID].DiscordID = id;
                moderatorData[player.userID].UserName = player.displayName;	
            }
            SendReply(player, $"Ваш дискорд: {id} Успешно добавлен.\nЧтобы изменить Discord ID введите /ds #ID, где #ID - ID в вашем дискорде.\nНапример: /ds EcoSmile#0001");
        }

        [ConsoleCommand("report")]
        void DrawMainUI_Comd(ConsoleSystem.Arg arg) => DrawMainUI(arg.Player());

        [ChatCommand("report")]
        void DrawMainUI(BasePlayer player)
        {
            var container = new CuiElementContainer();
            UI.AddImage(ref container, "Overlay", "MainReport.BG", "0 0 0 0.0", "", "", "0 0", "1 1", "", "");
            UI.AddImage(ref container, "MainReport.BG", "MainReportField", "0.36 0.34 0.32 0.85", "assets/content/ui/ui.background.tiletex.psd", "assets/icons/greyout.mat", "0.5 0.5", $"0.5 0.5", "-380 -250", "380 250");
            UI.AddImage(ref container, "MainReportField", "MainReportField.Header", "0.36 0.34 0.32 1",
                "assets/content/ui/ui.background.tiletex.psd", "assets/icons/greyout.mat", "0.5 1", $"0.5 1", "-380 -0", "380 55");
            UI.AddText(ref container, "MainReportField.Header", "Header.text", "0.68 0.63 0.60 1", "СИСТЕМА РЕПОРТОВ", TextAnchor.MiddleCenter, 32, "0.5 0.5", "0.5 0.5", "-250 -25", "250 25", "0 0 0 1", "0.1 0.1", "robotocondensed-bold.ttf", 1f);

            UI.AddButton(ref container, "MainReportField.Header", "closeBtn", $"", "MainReport.BG", "1 1 1 0", "", "", "1 1", "1 1", "-1300 -40", "500 180");
            UI.AddButton(ref container, "MainReportField.Header", "closeBtn", $"", "MainReport.BG", "1 1 1 0", "", "", "1 1", "1 1", "-1300 -800", "-760 180");
            UI.AddButton(ref container, "MainReportField.Header", "closeBtn", $"", "MainReport.BG", "1 1 1 0", "", "", "1 1", "1 1", "-1300 -800", "500 -555");
            UI.AddButton(ref container, "MainReportField.Header", "closeBtn", $"", "MainReport.BG", "1 1 1 0", "", "", "1 1", "1 1", "0 -550", "500 180");

            UI.AddCursor(ref container, "MainReport.BG");
            if (permission.UserHasPermission(player.UserIDString, config.ModeratorPermis))
            {
                UI.AddButton(ref container, "MainReportField.Header", "ModerMenu.BTN", "rs.moderatorpanel", "", "0.28 0.32 0.17 1", "", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "0 0.5", "0 0.5", "5 -20", "150 20");
                UI.AddText(ref container, "ModerMenu.BTN", "Btn.Text", "0.68 0.63 0.60 1", "РЕПОРТЫ", TextAnchor.MiddleCenter, 28, "0 0", "1 1", "", "", "0 0 0 0.5", "robotocondensed-regular.ttf");
            }
            if (permission.UserHasPermission(player.UserIDString, config.AdminPermis))
            {
                UI.AddButton(ref container, "MainReportField.Header", "ModerMenu.BTN", "rs.openadminpanel", "", "0.141 0.137 0.109 0.8", "", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "1 0.5", "1 0.5", "-205 -20", "-50 20");
                UI.AddText(ref container, "ModerMenu.BTN", "Btn.Text", "0.68 0.63 0.60 1", "АДМИН РАЗДЕЛ", TextAnchor.MiddleCenter, 16, "0 0", "1 1", "", "", "0 0 0 0.5", "robotocondensed-regular.ttf");
            }
            CuiHelper.DestroyUi(player, "MainReport.BG");
            CuiHelper.AddUi(player, container);
            DrawInputField(player);
            DrawPlayerList(player, 0, "");
        }
        void DrawInputField(BasePlayer player)
        {
            var container = new CuiElementContainer();
            UI.AddText(ref container, "MainReportField", "HeaderText", "0.68 0.63 0.60 1", "Введите Ник игрока или SteamID для поиска", TextAnchor.MiddleCenter, 18, "0.5 0.96", "0.5 0.96", "-250 -15", "250 15");
            UI.AddImage(ref container, "MainReportField", "InputName.bg", "0.68 0.63 0.60 0.50", "", "", "0.5 0.90", "0.5 0.90", "-250 -15", "250 15");
            UI.AddInputField(ref container, "InputName.bg", "InputName", $"rs.search", TextAnchor.MiddleCenter, 18, 30);

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("rs.openadminpanel")]
        void AdminPanel_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!permission.UserHasPermission(player.UserIDString, config.AdminPermis)) return;

            var container = new CuiElementContainer();
            UI.AddImage(ref container, "MainReport.BG", "MainReportField", "0.141 0.137 0.109 0.8",
                "assets/content/ui/ui.background.tiletex.psd", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "0.5 0.5", $"0.5 0.5", "-380 -250", "380 250");
            UI.AddImage(ref container, "MainReportField", "MainReportField.Header", "0.36 0.34 0.32 1",
                "assets/content/ui/ui.background.tiletex.psd", "assets/icons/greyout.mat", "0.5 1", $"0.5 1", "-380 -0", "380 55");
            UI.AddText(ref container, "MainReportField.Header", "Header.text", "0.68 0.63 0.60 1", "РАЗДЕЛ АДМИНИСТРАТОРА", TextAnchor.MiddleCenter, 32, "0.5 0.5", "0.5 0.5", "-250 -25", "250 25", "0 0 0 1", "0.1 0.1", "robotocondensed-bold.ttf", 1f);

            UI.AddButton(ref container, "MainReportField.Header", "closeBtn", $"report", "", "1 1 1 0.5", "assets/icons/close.png", "", "1 1", "1 1", "-40 -40", "-10 -10");

            CuiHelper.DestroyUi(player, "MainReportField");
            CuiHelper.AddUi(player, container);
            DrawAdminMenu(player);
        }

        void DrawAdminMenu(BasePlayer player)
        {
            var starthorisontal = 0.10;
            var startvertical = 0.82f;
            var vertical = startvertical;
            var horisontal = starthorisontal;
            var container = new CuiElementContainer();
            UI.AddImage(ref container, "MainReportField", "PlayerPanel", "1 1 1 0.0", "", "", "0.5 0.5", "0.5 0.5", "-380 -250", "380 250");
            int count = 0;
            foreach (var moderator in moderatorData)
            {
                var victum = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == moderator.Key);
                string colorStatys = victum != null ? "0.28 0.32 0.17 1" : "0.68 0.63 0.60 0.50";
                string color = count % 2 == 0 ? "0 0 0 0.5" : "0.929 0.882 0.847 0.15";
                UI.AddImage(ref container, "PlayerPanel", "Victum.Name.BG", color, "", "", $"{horisontal} {vertical}", $"{horisontal} {vertical}", $"-60 -105", "60 75");
                UI.AddRawImage(ref container, "Victum.Name.BG", "Avatar", ImageLibrary?.Call<string>("GetImage", moderator.Key.ToString()), "1 1 1 1", "", "", "0.5 1", "0.5 1", "-40 -85", "40 -5");
                UI.AddText(ref container, "Victum.Name.BG", "Victim.NameID", "0.68 0.63 0.60 1", $" {moderator.Value.UserName}\n[{moderator.Key}]\nПроверок выполнил: {moderator.Value.CheckCount}\nИгроков забанил: {moderator.Value.BanCount}", TextAnchor.UpperLeft, 10, "0.5 0", "0.5 0", "-59 1", "59 75");

                UI.AddText(ref container, "Victum.Name.BG", "Victim.Online", victum == null ? "1 0 0 0.3" : "0 1 0 0.3", $"• ", TextAnchor.UpperRight, 32, "0 0", "1 1", "", "");

                horisontal += 0.2f;
                if (horisontal > 1)
                {
                    vertical -= 0.5f;
                    horisontal = starthorisontal;
                }
                count++;
            }

            CuiHelper.DestroyUi(player, "PlayerPanel");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("rs.moderatorpanel")]
        void ModeratorPanel_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!permission.UserHasPermission(player.UserIDString, config.ModeratorPermis)) return;
            if (moderatorData.ContainsKey(player.userID) && string.IsNullOrEmpty(moderatorData[player.userID].DiscordID))
            {
                SendReply(player, $"Чтобы вести работу с системой репортов, необходимо подключить свой Discrod.\nВведите /ds #ID, где #ID - ID в вашем дискорде.\nНапример: /ds EcoSmile#0001");
                return;
            }
            if (!moderatorData.ContainsKey(player.userID))
            {
                SendReply(player, $"Чтобы вести работу с системой репортов, необходимо подключить свой Discrod.\nВведите /ds #ID, где #ID - ID в вашем дискорде.\nНапример: /ds EcoSmile#0001");
                return;
            }
            var container = new CuiElementContainer();
            UI.AddImage(ref container, "MainReport.BG", "MainReportField", "0.141 0.137 0.109 0.8",
                "assets/content/ui/ui.background.tiletex.psd", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "0.5 0.5", $"0.5 0.5", "-380 -250", "380 250");
            UI.AddImage(ref container, "MainReportField", "MainReportField.Header", "0.36 0.34 0.32 1",
                "assets/content/ui/ui.background.tiletex.psd", "assets/icons/greyout.mat", "0.5 1", $"0.5 1", "-380 -0", "380 55");
            UI.AddText(ref container, "MainReportField.Header", "Header.text", "0.68 0.63 0.60 1", "СПИСОК РЕПОРТОВ", TextAnchor.MiddleCenter, 32, "0.5 0.5", "0.5 0.5", "-250 -25", "250 25", "0 0 0 1", "0.1 0.1", "robotocondensed-bold.ttf", 1f);

            UI.AddButton(ref container, "MainReportField.Header", "closeBtn", $"report", "", "1 1 1 0.5", "assets/icons/close.png", "", "1 1", "1 1", "-40 -40", "-10 -10");

            CuiHelper.DestroyUi(player, "MainReportField");
            CuiHelper.AddUi(player, container);
            ReportsList(player);
        }

        void ReportsList(BasePlayer player, int page = 0)
        {
            var starthorisontal = 0.10;
            var startvertical = 0.42f;
            var vertical = startvertical;
            var horisontal = starthorisontal;
            var container = new CuiElementContainer();
            UI.AddImage(ref container, "MainReportField", "PlayerPanel", "1 1 1 0.0", "", "", "0.5 0.5", "0.5 0.5", "-380 -250", "380 250");
            int count = 0;
            var reportList = PlayerReports.Where(x => x.Value.Reports.Count() > 0).OrderBy(x => x.Value.Reports.Count);

            if (reportList.Count() == 0)
                UI.AddText(ref container, $"PlayerPanel", "NoPlayer", "0.68 0.63 0.60 1", "РЕПОРТОВ НЕТ", TextAnchor.MiddleCenter, 32, "0.1 0", "0.9 1", "", "");
            //var report = reportList.Skip(page * 8).Take(8).FirstOrDefault();
            foreach (var report in reportList.Skip(page * 10).Take(10))
            {
                string color = count % 2 == 0 ? "0 0 0 0.5" : "0.929 0.882 0.847 0.15";
                UI.AddImage(ref container, "PlayerPanel", "Victum.Name.BG", color, "", "", $"{horisontal} {vertical}", $"{horisontal} {vertical}", $"-60 -105", "60 75");
                UI.AddRawImage(ref container, "Victum.Name.BG", "Avatar", ImageLibrary?.Call<string>("GetImage", report.Key.ToString()), "1 1 1 1", "", "", "0.5 1", "0.5 1", "-40 -85", "40 -5");
                UI.AddText(ref container, "Avatar", "Victim.NameID", "0.68 0.63 0.60 1", $" {report.Value.UserName}\n[{report.Key}]\nРепортов: {report.Value.Reports.Count(x => x.status == ReportStatys.Open)} ({report.Value.Reports.Count})", TextAnchor.UpperLeft, 12, "0.5 0", "0.5 0", "-59 -65", "59 -5");

                var victum = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == report.Key);
                UI.AddText(ref container, "Avatar", "Victim.Online", victum == null ? "1 0 0 0.3" : "0 1 0 0.3", $"•", TextAnchor.MiddleCenter, 32, "1 1", "1 1", "0 -25", "20 15");

                UI.AddButton(ref container, "Avatar", "OpenButton", $"openreportlist {report.Key}", "", "0.341 0.137 0.109 0.8", "", "", "0.5 0", "0.5 0", "-55 -90", "55 -60");
                UI.AddText(ref container, "OpenButton", "Title", "1 1 1 0.65", "ПОСМОТРЕТЬ", TextAnchor.MiddleCenter, 14, "0 0", "1 1", "", "");

                horisontal += 0.2f;
                if (horisontal > 1)
                {
                    vertical -= 0.5f;
                    horisontal = starthorisontal;
                }
                count++;
            }
            if (page > 0)
            {
                UI.AddButton(ref container, "PlayerPanel", "BackBtn", $"rs.moderatorpage {page - 1}", "", "0.28 0.32 0.17 1", "", "", "0 0", "0 0", "5 5", "60 30");
                UI.AddText(ref container, $"BackBtn", "Text", "0.68 0.63 0.60 0.50", "◄", TextAnchor.MiddleCenter, 18, "0 0", "1 1", "", "");
            }

            if (reportList.Skip(page * 10).Count() > 10)
            {
                UI.AddButton(ref container, "PlayerPanel", "NextBtn", $"rs.moderatorpage {page + 1}", "", "0.28 0.32 0.17 1", "", "", "1 0", "1 0", "-60 5", "-5 30");
                UI.AddText(ref container, $"NextBtn", "Text", "0.68 0.63 0.60 0.50", "►", TextAnchor.MiddleCenter, 18, "0 0", "1 1", "", "");
            }

            CuiHelper.DestroyUi(player, "PlayerPanel");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("openreportlist")]
        void OpenReportList(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var reportedID = arg.GetUInt64(0);
            DrawPersonalReportList(player, reportedID, 0);
        }

        void DrawPersonalReportList(BasePlayer player, ulong reportedID, int page)
        {
            var reportData = PlayerReports[reportedID].Reports.OrderBy(x => x.status);
            var starthorisontal = 0.0;
            var startvertical = 1f;
            var vertical = startvertical;
            var horisontal = starthorisontal;
            var container = new CuiElementContainer();
            UI.AddImage(ref container, "MainReportField", "PlayerPanel", "1 1 1 0.0", "", "", "0.5 0.5", "0.5 0.5", "-380 -250", "380 100");
            UI.AddImage(ref container, "MainReportField", "RCCInfoPanel", "1 1 1 0.1", "", "", "0.5 0.5", "0.5 0.5", "-380 100", "380 250");
            UI.AddText(ref container, "RCCInfoPanel", "Reason", "0.68 0.63 0.60 1", $"ЗАПРОС ОТПРАВЛЕН, ОЖИДАЕМ ОТВЕТА ОТ RCC", TextAnchor.MiddleCenter, 16, "0 0", "1 1", "", "");
            int count = 0;
            var victum = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == reportedID);
            foreach (var report in reportData.Skip(page * 6).Take(6))
            {
                string color = count % 2 == 0 ? "0 0 0 0.5" : "0.929 0.882 0.847 0.15";
                string colorStatys = report.status == ReportStatys.Open ? "0.28 0.32 0.17 1" : "0.68 0.63 0.60 0.50";
                UI.AddImage(ref container, "PlayerPanel", "Victum.Name.BG", color, "", "", $"{horisontal} {vertical}", $"{horisontal} {vertical}", $"5 -55", "205 -5");
                UI.AddText(ref container, "Victum.Name.BG", "Victim.NameID", "0.68 0.63 0.60 1", $" {report.VictimName}\n[{reportedID}]", TextAnchor.MiddleLeft, 12, "0 0", "1 1", "", "");

                UI.AddText(ref container, "Victum.Name.BG", "Victim.Online", victum == null ? "1 0 0 0.3" : "0 1 0 0.3", $"• ", TextAnchor.UpperRight, 32, "0 0", "1 1", "", "");

                UI.AddImage(ref container, "Victum.Name.BG", "ReportReason.BG", color, "", "", $"1 0.5", $"1 0.5", $"5 -25", "125 25");
                UI.AddText(ref container, "ReportReason.BG", "Reason", "0.68 0.63 0.60 1", $"{report.ReportReson}", TextAnchor.MiddleCenter, 16, "0 0", "1 1", "", "");
                UI.AddImage(ref container, "ReportReason.BG", $"ReportData.BG", color, "", "", $"1 0.5", $"1 0.5", $"5 -25", "170 25");
                UI.AddText(ref container, "ReportData.BG", "ReportData", "0.68 0.63 0.60 1", $"{report.ReportDate.ToString("dd.MM.yyyy")}", TextAnchor.MiddleCenter, 16, "0 0", "1 1", "", "");
                UI.AddImage(ref container, "ReportData.BG", $"ReportStatys.BG", color, "", "", $"1 0.5", $"1 0.5", $"5 -25", "125 25");
                UI.AddText(ref container, "ReportStatys.BG", "Statys", colorStatys, $"{report.status.ToString().ToUpper()}", TextAnchor.MiddleCenter, 18, "0 0", "1 1", "", "");
                UI.AddImage(ref container, "ReportStatys.BG", $"GetReport.BG", color, "", "", $"1 0.5", $"1 0.5", $"5 -25", "125 25");

                if (victum != null)
                {
                    if (report.status == ReportStatys.Closed)
                    {
                        UI.AddButton(ref container, "GetReport.BG", "OpenReport.BTN", "", "", "1 1 1 0.05", "", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "0.5 0.5", "0.5 0.5", "-50 -20", "50 20");
                        UI.AddText(ref container, "OpenReport.BTN", "OpenReport.Text", "0.68 0.63 0.60 1", "ПРОВЕРЕНО", TextAnchor.MiddleCenter, 16, "0 0", "1 1", "", "", "0 0 0 0.5", "robotocondensed-regular.ttf");
                    }
                    else
                    {
                        UI.AddButton(ref container, "GetReport.BG", "OpenReport.BTN", report.status == ReportStatys.Open ? $"rs.openreport {report.ReportID} {reportedID}" : "", "", report.status == ReportStatys.Open ? "0.28 0.32 0.17 1" : "0.28 0.32 0.17 0.5", "", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "0.5 0.5", "0.5 0.5", "-50 -20", "50 20");
                        UI.AddText(ref container, "OpenReport.BTN", "OpenReport.Text", "0.68 0.63 0.60 1", "НАЧАТЬ ПРОВЕРКУ", TextAnchor.MiddleCenter, 16, "0 0", "1 1", "", "", "0 0 0 0.5", "robotocondensed-regular.ttf");
                    }
                }
                else
                {
                    UI.AddButton(ref container, "GetReport.BG", "OpenReport.BTN", report.status == ReportStatys.Open ? $"rs.banplayer {victum.userID}" : "", "", "0.341 0.137 0.109 0.8", "", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "0.5 0.5", "0.5 0.5", "-50 -20", "50 20");
                    UI.AddText(ref container, "OpenReport.BTN", "OpenReport.Text", "0.68 0.63 0.60 1", "БАН", TextAnchor.MiddleCenter, 16, "0 0", "1 1", "", "", "0 0 0 0.5", "robotocondensed-regular.ttf");
                }
                vertical -= 0.15f;
                count++;
            }
            if (page > 0)
            {
                UI.AddButton(ref container, "PlayerPanel", "BackBtn", $"rs.userreportlist {reportedID} {page - 1}", "", "0.28 0.32 0.17 1", "", "", "0 0", "0 0", "5 5", "60 30");
                UI.AddText(ref container, $"BackBtn", "Text", "0.68 0.63 0.60 0.50", "◄", TextAnchor.MiddleCenter, 18, "0 0", "1 1", "", "");
            }

            if (reportData.Skip(page * 6).Count() > 6)
            {
                UI.AddButton(ref container, "PlayerPanel", "NextBtn", $"rs.userreportlist {reportedID} {page + 1}", "", "0.28 0.32 0.17 1", "", "", "1 0", "1 0", "-60 5", "-5 30");
                UI.AddText(ref container, $"NextBtn", "Text", "0.68 0.63 0.60 0.50", "►", TextAnchor.MiddleCenter, 18, "0 0", "1 1", "", "");
            }

            CuiHelper.DestroyUi(player, "PlayerPanel");
            CuiHelper.AddUi(player, container);
            DrawRCCInfo(player, victum.userID);
        }

        void DrawRCCInfo(BasePlayer player, ulong userID)
        {

            var container = new CuiElementContainer();
            UI.AddImage(ref container, "MainReportField", "RCCInfoPanel", "1 1 1 0.1", "", "", "0.5 0.5", "0.5 0.5", "-380 100", "380 250");
            webrequest.Enqueue($"https://rustcheatcheck.ru/panel/api?action=getInfo&key={config.RCCSettings.APIKey}&player={userID}", null, (code, data) =>
            {
                if (code != 200)
                {
                    UI.AddText(ref container, "RCCInfoPanel", "Reason", "0.68 0.63 0.60 1", $"ОШИБКА: {code}\nОбратитесь к администратору", TextAnchor.MiddleCenter, 16, "0 0", "1 1", "", "");
                    CuiHelper.DestroyUi(player, "RCCInfoPanel");
                    CuiHelper.AddUi(player, container);
                    return;
                }
                RCCData rcc = JsonConvert.DeserializeObject<RCCData>(data);
                if (rcc == null)
                {
                    UI.AddText(ref container, "RCCInfoPanel", "Reason", "0.68 0.63 0.60 1", $"ИНФОРМАЦИЯ ОБ ИГРОКЕ ОТСУТСВУЕТ", TextAnchor.MiddleCenter, 16, "0 0", "1 1", "", "");

                    CuiHelper.DestroyUi(player, "RCCInfoPanel");
                    CuiHelper.AddUi(player, container);
                    return;
                }
                if (rcc.status == "error")
                {
                    UI.AddText(ref container, "RCCInfoPanel", "Reason", "0.68 0.63 0.60 1", $"Игрок не вызывался на проверку и баны отсутствуют", TextAnchor.MiddleCenter, 16, "0 0", "1 1", "", "");
                    CuiHelper.DestroyUi(player, "RCCInfoPanel");
                    CuiHelper.AddUi(player, container);
                    return;
                }
                if (rcc.status == "success")
                {
                    if (rcc.bans.Count > 0)
                    {
                        string reason = " Информация о банах игрока на сторонних проектах:\n";
                        foreach (var info in rcc.bans.OrderByDescending(x => x.banDate))
                        {
                            var dTime = new DateTime(1970, 1, 1, 0, 0, 0);
                            var unbanData = new TimeSpan(0, 0, int.Parse(info.unbanDate));
                            var endData = dTime.Add(unbanData);

                            reason += $"    [{info.serverName}] Дата бана: {new DateTime(1970, 1, 1, 0, 0, 0, 0).Add(new TimeSpan(0, 0, int.Parse(info.banDate))).ToString("dd.MM.yyyy")}, Дата разбана: {(info.unbanDate == "0" ? "ПЕРМАНЕНТ" : new DateTime(1970, 1, 1, 0, 0, 0, 0).Add(unbanData).ToString("dd.MM.yyyy"))}, Причина: <color=#f44e42>{info.reason}</color>, Статус: {(info.active ? "<color=green>АКТИВЕН</color>" : "Не активен")}\n";

                        }
                        UI.AddText(ref container, "RCCInfoPanel", "Reason", "0.68 0.63 0.60 1", $"{reason}", TextAnchor.MiddleLeft, 13, "0 0", "1 1", "", "");

                    }
                    else
                    {
                        UI.AddText(ref container, "RCCInfoPanel", "Reason", "0.68 0.63 0.60 1", $"Баны на сторонних проектах не обнаружены", TextAnchor.MiddleCenter, 16, "0 0", "1 1", "", "");
                    }
                }
                CuiHelper.DestroyUi(player, "RCCInfoPanel");
                CuiHelper.AddUi(player, container);

            }, this);

        }
        [PluginReference]
        Plugin NoEscape;

        [ConsoleCommand("rs.openreport")]
        void OpenReport(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var reportID = arg.Args[0];
            var userID = arg.GetUInt64(1);

            var report = PlayerReports[userID].Reports.FirstOrDefault(x => x.ReportID == reportID);
            var victim = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == userID);

            if (victim.GetComponent<ReportPlayer>())
            {
                SendReply(player, "Игрок уже проверяется");
                return;
            }

            if (NoEscape?.Call<bool>("IsRaidBlock", victim.userID) == true)
            {
                SendReply(player, "Игрок находится в Рейде, дождитесь окончания рейда и попробуйте снова.");
                return;
            }

            CuiHelper.DestroyUi(player, "MainReport.BG");

            UpdateReportList(victim.userID, ReportStatys.Process);

            var component = victim.gameObject.AddComponent<ReportPlayer>();
            component.Init(player);
            BanProcess[player.IPlayer] = new BanInfo(victim.userID, victim.displayName, report.ReportID);
            BanProcess[player.IPlayer].component = component;
            DrawAlert(victim, player);

            SendReply(player, $"Вы успешно вызвали игрока {victim.displayName} на проверку.\nПожалуйста, ожидайте, пока игрок добавит Вас в Discord.");

            SendDiscordMsg($"Модератор {player.displayName} ({player.userID}) вызвал на проверку игрока {victim.displayName} ({victim.userID}).\nReport ID {report.ReportID}", false);
            SendVKMsg($"Модератор {player.displayName} ({player.userID}) вызвал на проверку игрока {victim.displayName} ({victim.userID}).\nReport ID {report.ReportID}", config.VKSettings.LogChatID);
            PrintToChat($"Игрок {victim.displayName} ({victim.userID}) вызван на проверку Модератором {player.displayName} ({player.userID})");
        }

        private void DrawAlert(BasePlayer player, BasePlayer moderator)
        {
            var container = new CuiElementContainer();

            UI.AddImage(ref container, "Overlay", "ReportAlert", "0.36 0.34 0.32 0.9", "assets/content/ui/ui.background.tiletex.psd", "assets/icons/greyout.mat", "0.5 0.8", "0.5 0.8", "-350 -60", "350 60");
            UI.AddText(ref container, "ReportAlert", "ReportText", "1 1 1 0.65", $"Здравствуйте, <b><color=#f44e42>{player.displayName}</color></b>, Администрацией проекта было принято решение проверить Вас на наличие запрещенного ПО в связи с большим количеством жалоб.\n" +
                $"Выход из сети во время проверки приведет к Бану.\nПожалуйста добавьте проверяющего в Discord: <b><color=#f44e42>{(moderatorData.ContainsKey(moderator.userID) ? moderatorData[moderator.userID].DiscordID : "#err")}</color></b>\nУ Вас есть {config.DsAddTime} сек чтобы добавить Проверяющего.", TextAnchor.MiddleCenter, 18, "0 0", "1 1", "", "");
            Effect effect = new Effect("ASSETS/BUNDLED/PREFABS/FX/INVITE_NOTICE.PREFAB".ToLower(), player, 0, new Vector3(), new Vector3());
            EffectNetwork.Send(effect, player.Connection);
            CuiHelper.DestroyUi(player, "ReportAlert");
            CuiHelper.AddUi(player, container);
        }

        void UpdateReportList(ulong userID, ReportStatys status)
        {
            if (!PlayerReports.ContainsKey(userID)) return;
            foreach (var report in PlayerReports[userID].Reports.Where(x => x.status != ReportStatys.Closed))
            {
                report.status = status;
            }
        }

        [ConsoleCommand("rs.userreportlist")]
        void UserReportPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var reportedID = arg.GetUInt64(0);
            var page = arg.GetInt(1);
            if (page < 0)
                page = 0;
            DrawPersonalReportList(player, reportedID, page);
        }

        [ConsoleCommand("rs.moderatorpage")]
        void ModeratorPages(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var page = arg.GetInt(0);
            if (page < 0)
                page = 0;
            ReportsList(player, page);
        }

        [ConsoleCommand("rs.search")]
        void SerchPlayer(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var NameOrID = string.Join("", arg.Args);
            DrawPlayerList(player, 0, NameOrID);
        }

        void DrawPlayerList(BasePlayer player, int page, string NameOrID)
        {
            var starthorisontal = 0.00;
            var startvertical = 0.91f;
            var vertical = startvertical;
            var horisontal = starthorisontal;

            var playerList = BasePlayer.activePlayerList.Where(x => x.userID != player.userID).Skip(page * 15).Take(15).ToList();
            var FindUsers = new List<Core.Libraries.Covalence.IPlayer>();
            if (!string.IsNullOrEmpty(NameOrID))
                FindUsers = covalence.Players.FindPlayers(NameOrID).ToList();

            if (FindUsers != null && FindUsers.Count > 0)
            {
                playerList = new List<BasePlayer>();
                foreach (var user in FindUsers)
                {
                    playerList.Add(user.Object as BasePlayer);
                }
            }
            var navContainer = new CuiElementContainer();
            UI.AddImage(ref navContainer, "MainReportField", "PlayerPanel", "1 1 1 0.0", "", "", "0.5 0.5", "0.5 0.5", "-380 -250", "380 180");
            if (playerList.Count() == 0 || (playerList.Where(x => x != null).Count() <= 0 && !string.IsNullOrEmpty(NameOrID)))
            {
                if (playerList.Where(x => x != null).Count() <= 0 && !string.IsNullOrEmpty(NameOrID))
                {
                    UI.AddText(ref navContainer, $"PlayerPanel", "NoPlayer", "0.68 0.63 0.60 1", $"Игрок {NameOrID} не найден.", TextAnchor.MiddleCenter, 22, "0 0", "1 1", "", "");
                }
                else if (playerList.Count() == 0)
                    UI.AddText(ref navContainer, $"PlayerPanel", "NoPlayer", "0.68 0.63 0.60 1", "КРОМЕ ВАС ИГРОКОВ НА СЕРВЕРЕ НЕТ", TextAnchor.MiddleCenter, 22, "0.5 0.5", "0.5 0.5", "-300 -100", "300 200");
            }

            if (page > 0)
            {
                UI.AddButton(ref navContainer, "PlayerPanel", "BackBtn", $"rs.reportpage {page - 1}", "", "0.28 0.32 0.17 1", "", "", "0 0", "0 0", "5 5", "60 30");
                UI.AddText(ref navContainer, $"BackBtn", "Text", "0.68 0.63 0.60 0.50", "◄", TextAnchor.MiddleCenter, 18, "0 0", "1 1", "", "");
            }

            if (BasePlayer.activePlayerList.Where(x => x.userID != player.userID).Skip(page * 18).Count() > 18)
            {
                UI.AddButton(ref navContainer, "PlayerPanel", "NextBtn", $"rs.reportpage {page + 1}", "", "0.28 0.32 0.17 1", "", "", "1 0", "1 0", "-60 5", "-5 30");
                UI.AddText(ref navContainer, $"NextBtn", "Text", "0.68 0.63 0.60 0.50", "►", TextAnchor.MiddleCenter, 18, "0 0", "1 1", "", "");
            }

            CuiHelper.DestroyUi(player, "PlayerPanel");
            CuiHelper.AddUi(player, navContainer);

            foreach (var pl in playerList.Where(x => x != null))
            {
                var userID = pl.userID;
                var container = new CuiElementContainer();
                UI.AddImage(ref container, "PlayerPanel", $"Player.container.{userID}", "0.68 0.63 0.60 0.50", "", "", $"{horisontal} {vertical}", $"{horisontal} {vertical}", "0 -35", "210 35");
                UI.AddRawImage(ref container, $"Player.container.{userID}", "Avatar", ImageLibrary?.Call<string>("GetImage", userID.ToString()), "1 1 1 1", "", "", "0.0 0.5", "0.0 0.5", "2 -32", "64 32");
                UI.AddText(ref container, $"Player.container.{userID}", "Player.Name", "0.68 0.63 0.60 1", $"{pl.displayName}", TextAnchor.MiddleLeft, 18, "0.65 1", "0.65 1", "-70 -25", "70 0");
                UI.AddButton(ref container, $"Player.container.{userID}", $"ReportBtn.{userID}", $"rs.openreport.ui {userID} false", "", "0.28 0.32 0.17 0", "", "", "1 0.5", "1 0.5", "-210 -32", "-2 32");
                UI.AddText(ref container, $"ReportBtn.{userID}", "Text", "0.68 0.63 0.60 0.50", "", TextAnchor.MiddleCenter, 18, "0 0", "1 1", "", "");

                CuiHelper.AddUi(player, container);

                horisontal += 0.35f;
                if (horisontal >= 0.9f)
                {
                    horisontal = starthorisontal;
                    vertical -= 0.18f;
                }
            }
        }

        [ConsoleCommand("rs.reportpage")]
        void ReportPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var page = arg.GetInt(0);
            if (page < 0)
                page = 0;
            DrawPlayerList(player, page, "");
        }

        Dictionary<BasePlayer, DateTime> reportCD = new Dictionary<BasePlayer, DateTime>();

        [ChatCommand("stop")]
        void StopCommand(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, config.ModeratorPermis)) return;
            var process = BanProcess[player.IPlayer];
            UncheckPlayer(player, process.userID);
        }

        [ChatCommand("check")]
        void StartManualPlayerCheck(BasePlayer player, string command, string[] arg)
        {
            if (!permission.UserHasPermission(player.UserIDString, config.ModeratorPermis)) return;
            if(!moderatorData.ContainsKey(player.userID))
            {
                if (moderatorData.ContainsKey(player.userID) && string.IsNullOrEmpty(moderatorData[player.userID].DiscordID))
                    SendReply(player, $"Чтобы вести работу с системой репортов, необходимо подключить свой Discrod.\nВведите /ds #ID, где #ID - ID в вашем дискорде.\nНапример: /ds EcoSmile#0001");
                else if (!moderatorData.ContainsKey(player.userID))
                    SendReply(player, $"Чтобы вести работу с системой репортов, необходимо подключить свой Discrod.\nВведите /ds #ID, где #ID - ID в вашем дискорде.\nНапример: /ds EcoSmile#0001");

                return;
            }
            var targetUserID = string.Join(" ", arg);
            var victims = covalence.Players.FindPlayers(targetUserID).ToList();
            if(victims == null || victims.Count() <= 0)
            {
                SendReply(player, $"Игрок с именем {targetUserID} не найден");
                return;
            }
            if(victims.Count() > 1)
            {
                SendReply(player, $"Найдено несколько игроков с таким именем:\n{string.Join(" ", victims.Select(x=>x.Name))}");
                return;
            }
            if ((victims[0].Object as BasePlayer).GetComponent<ReportPlayer>())
            {
                SendReply(player, "Игрок уже проверяется");
                return;
            }

            if (NoEscape?.Call<bool>("IsRaidBlock", (victims[0].Object as BasePlayer).userID) == true)
            {
                SendReply(player, "Игрок находится в Рейде, дождитесь окончания рейда и попробуйте снова.");
                return;
            }

            CuiHelper.DestroyUi(player, "MainReport.BG");
            UpdateReportList(player.userID, ReportStatys.Process);

            Report report = null;
            if(PlayerReports.ContainsKey((victims[0].Object as BasePlayer).userID))
                report = PlayerReports[(victims[0].Object as BasePlayer).userID].Reports.FirstOrDefault(x => x.status == ReportStatys.Process);

            if (report == null)
            {
                report = new Report()
                { 
                    ReportDate = DateTime.Now,
                    VictimName = (victims[0].Object as BasePlayer).displayName,
                    ReportID = UnityEngine.Random.Range(1, 9999999).ToString(),
                    SenderName = player.displayName,
                    ReportReson = "ПРОВЕРКА МОДЕРАТОРОМ",
                    status = ReportStatys.Open,
                    SendreID = player.userID,
                };
            }
            BanProcess[player.IPlayer] = new BanInfo((victims[0].Object as BasePlayer).userID, (victims[0].Object as BasePlayer).displayName, report.ReportID);

            if (PlayerReports.ContainsKey((victims[0].Object as BasePlayer).userID))
                PlayerReports[(victims[0].Object as BasePlayer).userID].Reports.Add(report);
            else
            {
                PlayerReports[(victims[0].Object as BasePlayer).userID] = new UserReportData();
                PlayerReports[(victims[0].Object as BasePlayer).userID].UserName = (victims[0].Object as BasePlayer).displayName;
                PlayerReports[(victims[0].Object as BasePlayer).userID].Reports = new List<Report>();
                PlayerReports[(victims[0].Object as BasePlayer).userID].Reports.Add(report);
            }

            var component = (victims[0].Object as BasePlayer).gameObject.AddComponent<ReportPlayer>();
            component.Init(player);
            BanProcess[player.IPlayer].component = component;
            DrawAlert((victims[0].Object as BasePlayer), player);

            SendDiscordMsg($"Модератор {player.displayName} ({player.userID}) вызвал на проверку игрока {(victims[0].Object as BasePlayer).displayName} ({(victims[0].Object as BasePlayer).userID}).\nReport ID {report.ReportID}", false);
            SendVKMsg($"Модератор {player.displayName} ({player.userID}) вызвал на проверку игрока {(victims[0].Object as BasePlayer).displayName} ({(victims[0].Object as BasePlayer).userID}).\nReport ID {report.ReportID}", config.VKSettings.LogChatID);
            PrintToChat($"Игрок {(victims[0].Object as BasePlayer).displayName} ({(victims[0].Object as BasePlayer).userID}) вызван на проверку Модератором {player.displayName}");
        }

        [ConsoleCommand("rs.openreport.ui")]
        void OpenReportUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            CuiHelper.DestroyUi(player, "DeatReport.BTN");
            var victumID = arg.GetUInt64(0);
            bool IsOwner = arg.GetBool(1);
		Effect effect = new Effect("assets/prefabs/tools/detonator/effects/unpress.prefab", player, 0, new Vector3(), new Vector3());
                EffectNetwork.Send(effect, player.Connection);

            if (reportCD.ContainsKey(player) && reportCD[player] > DateTime.Now)
            {
                SendReply(player, $"Нельзя отправлять репорты так часто.\nПодождите: {(DateTime.Now - reportCD[player]).ToShortString()}");
                return;
            }

            var container = new CuiElementContainer();

            if (IsOwner)
                UI.AddImage(ref container, "Overlay", "MainReport.BG", "0 0 0 0.2", "", "assets/content/ui/uibackgroundblur.mat", "0 0", "1 1", "", "");


            UI.AddImage(ref container, "MainReport.BG", "Selector.BG", "0 0 0 0.2", "", "assets/content/ui/uibackgroundblur.mat", "0 0", "1 1", "", "");

            var startheight = 200;

            UI.AddImage(ref container, "Selector.BG", "ReportSelector.Field", "0.36 0.34 0.32 0",
                "assets/content/ui/ui.background.tiletex.psd", "assets/icons/greyout.mat", "0.5 0.5", $"0.5 0.5", $"-150 -{startheight}", $"150 {startheight}");

            UI.AddImage(ref container, "ReportSelector.Field", "ReportSelector.Header", "0.36 0.34 0.32 0",
                "assets/content/ui/ui.background.tiletex.psd", "assets/icons/greyout.mat", "0.5 1", $"0.5 1", "-150 -0", "150 55");
            UI.AddText(ref container, "ReportSelector.Header", "Header.text", "0.68 0.63 0.60 1", "ПРИЧИНЫ", TextAnchor.MiddleCenter, 32, "0.5 0.5", "0.5 0.5", "-150 -25", "150 25", "0 0 0 1", "0.1 0.1", "robotocondensed-bold.ttf", 1f);

            UI.AddButton(ref container, "ReportSelector.Header", "closeBtn", $"", IsOwner ? "MainReport.BG" : "Selector.BG", "1 1 1 0", "", "", "1 1", "1 1", "-4000 -800", "1000 300");

            UI.AddText(ref container, "ReportSelector.Field", "HeaderText", "0.68 0.63 0.60 0.50", "ВЫБЕРИТЕ ЧТО НАРУШИЛ ДАННЫЙ ИГРОК", TextAnchor.MiddleCenter, 16, "0.5 1", "0.5 1", "-150 -55", "150 -5");

            var starthorisontal = 0.90;
            var startvertical = 0.8f;
            var vertical = startvertical;
            var horisontal = starthorisontal;
            var index = 0;
            foreach (var rtype in config.Reasons)
            {
                UI.AddButton(ref container, "ReportSelector.Field", "report.reason", $"rs.sendreport {victumID} {index}", "", index % 2 == 0 ? "0.36 0.34 0.34 1" : "0.36 0.34 0.34 1", "assets/content/ui/ui.background.tiletex.psd", "assets/icons/greyout.mat", $"{horisontal} {vertical}", $"{horisontal} {vertical}", "-100 -15", "100 15");
                UI.AddText(ref container, "report.reason", "text", "0.68 0.63 0.60 0.50", $"{rtype.ToUpper()}", TextAnchor.MiddleCenter, 16, "0 0", "1 1", "", "");
                vertical -= 0.1f;
                index++;
            }
            CuiHelper.DestroyUi(player, "ReportSelector.Field");
            CuiHelper.AddUi(player, container);
        }

        void OnPlayerReported(BasePlayer reporter, string targetName, string targetId, string subject, string message, string type)
        {
            var victum = BasePlayer.activePlayerList.FirstOrDefault(x => x.UserIDString == targetId);

            if (victum != null && victum.GetComponent<ReportPlayer>())
            {
                return;
            }

            var report = new Report();
            report.VictimName = targetName;
            report.ReportID = UnityEngine.Random.Range(1, 9999999).ToString();
            report.SenderName = reporter.displayName;
            report.ReportDate = DateTime.Now;
            report.ReportReson = "[RUST]: " + subject;
            report.status = ReportStatys.Open;
            report.SendreID = reporter.userID;
            var userID = ulong.Parse(targetId);
            if (PlayerReports.ContainsKey(userID))
                PlayerReports[userID].Reports.Add(report);
            else
            {
                PlayerReports[userID] = new UserReportData();
                PlayerReports[userID].UserName = targetName;
                PlayerReports[userID].Reports = new List<Report>();
                PlayerReports[userID].Reports.Add(report);
            }

            string reportMessage = $"" +
                $"Игрок {reporter.displayName} [{reporter.userID}] отправил репорт на игрока {report.VictimName} [{userID}]." +
                $"\nПричина: {report.ReportReson}." +
                $"\nReport ID: {report.ReportID}." +
                $"\nКоличество открытых репортов на игрока: {PlayerReports[userID].Reports.Count(x => x.status == ReportStatys.Open)}";

            PrintWarning(reportMessage);
            SendDiscordMsg(reportMessage, true);
            SendVKMsg(reportMessage, config.VKSettings.ChatID);
        }

        [ConsoleCommand("rs.sendreport")]
        void SendReport(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var victumID = arg.GetUInt64(0);
            var reseonIndex = arg.GetInt(1);
            CuiHelper.DestroyUi(player, "MainReport.BG");
            var victum = BasePlayer.FindByID(victumID);
            if (victum == null) return;

            reportCD[player] = DateTime.Now.AddSeconds(config.ReportCD);

            var report = new Report();
            report.VictimName = victum.displayName;
            report.ReportID = UnityEngine.Random.Range(1, 9999999).ToString();
            report.SenderName = player.displayName;
            report.ReportDate = DateTime.Now;
            report.ReportReson = config.Reasons[reseonIndex];
            report.status = ReportStatys.Open;
            report.SendreID = player.userID;

            if (PlayerReports.ContainsKey(victum.userID))
                PlayerReports[victum.userID].Reports.Add(report);
            else
            {
                PlayerReports[victum.userID] = new UserReportData();
                PlayerReports[victum.userID].UserName = victum.displayName;
                PlayerReports[victum.userID].Reports = new List<Report>();
                PlayerReports[victum.userID].Reports.Add(report);
            }

            string reportMessage = $"" +
                $"Игрок {report.SenderName} [{player.userID}] отправил репорт на игрока {report.VictimName} [{victum.userID}]." +
                $"\nПричина: {report.ReportReson}." +
                $"\nReport ID: {report.ReportID}." +
                $"\nКоличество открытых репортов на игрока: {PlayerReports[victum.userID].Reports.Count(x => x.status == ReportStatys.Open)}";

            PrintWarning(reportMessage);
            SendDiscordMsg(reportMessage, true);
            SendVKMsg(reportMessage, config.VKSettings.ChatID);
            SendReply(player, $"Репорт успешно отправлен.");
        }

        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return null;
            var attaker = info.InitiatorPlayer;
            if (attaker != null && attaker.userID.IsSteamId())
            {
                var attakerID = attaker.userID;
                if (attaker == player) return null;
                DrawDeathButton(player, attakerID);
            }
            return null;
        }

        void DrawDeathButton(BasePlayer player, ulong AttakerID)
        {
            if (!config.DeathButton) return;
            var container = new CuiElementContainer();
            UI.AddButton(ref container, "Overlay", "DeatReport.BTN", $"brs.openreport.ui {AttakerID} true", "", "0.341 0.137 0.109 0.8",
                "assets/content/ui/ui.background.tiletex.psd", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "1 1", "1 1", "-150 -75", "-10 -25");
            UI.AddText(ref container, "DeatReport.BTN", "Death.text", "0.68 0.63 0.60 1", "REPORT", TextAnchor.MiddleCenter, 28, "0 0", "1 1", "", "");
            CuiHelper.DestroyUi(player, "DeatReport.BTN");
            timer.In(2f, () => CuiHelper.AddUi(player, container));

        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "DeatReport.BTN");
        }

        void OnClientAuth(Connection connection)
        {
            timer.In(3f, () =>
            {
                if (connection == null) return;
                GetUserInfo(connection.userid, connection.username, (x, reason) =>
                {
                    if (!x)
                    {
                        if (connection == null) return;
                        if (config.RCCSettings.KickPlayers)
                            ConnectionAuth.Reject(connection, reason);
                    }
                });
            });
        }

        [ConsoleCommand("rs.banplayer")]
        void BanPlayer_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!permission.UserHasPermission(player.UserIDString, config.ModeratorPermis)) return;
            var targetID = arg.GetUInt64(0);
            //BanPlayer(player, targetID);

            OpenBanUI(player, targetID);
        }
        public class BanInfo
        {
            public string BanReason;
            public string BanTime;
            public ulong userID;
            public string UserName;
            public string ReportID;
            public ReportPlayer component;

            public BanInfo(ulong ID, string Name, string reportID)
            {
                userID = ID;
                UserName = Name;
                BanReason = "default";
                BanTime = "";
                ReportID = reportID;
            }
        }

        Dictionary<IPlayer, BanInfo> BanProcess = new Dictionary<IPlayer, BanInfo>();

        void OpenBanUI(BasePlayer player, ulong targetUserID)
        {
            var victum = BasePlayer.Find(targetUserID.ToString());
            UpdateReportList(player.userID, ReportStatys.Process);
            var report = PlayerReports[targetUserID].Reports.FirstOrDefault(x => x.status == ReportStatys.Process);
            if (report == null)
            {
                report = new Report()
                {
                    ReportDate = DateTime.Now,
                    VictimName = victum.displayName,
                    ReportID = UnityEngine.Random.Range(1, 9999999).ToString(),
                    SenderName = player.displayName,
                    ReportReson = "ПРОВЕРКА МОДЕРАТОРОМ",
                    status = ReportStatys.Open,
                    SendreID = player.userID,
                };
            }
            BanProcess[player.IPlayer] = new BanInfo(targetUserID, victum.displayName,report.ReportID);

            var container = new CuiElementContainer();
            UI.AddImage(ref container, "Overlay", "MainBan.BG", "0 0 0 0.2", "", "assets/content/ui/uibackgroundblur.mat", "0 0", "1 1", "", "");
            UI.AddCursor(ref container, "MainBan.BG");
            UI.AddImage(ref container, "MainBan.BG", "Ban.Field", "0.141 0.137 0.109 0.8",
                "assets/content/ui/ui.background.tiletex.psd", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "0.5 0.5", $"0.5 0.5", $"-150 -350", $"150 250");

            UI.AddImage(ref container, "Ban.Field", "o.l.1", "0.63 0.77 0.29 0.5", "", "", "1 0.5", "1 0.5", "0 -302", "2 302");
            UI.AddImage(ref container, "Ban.Field", "o.l.2", "0.63 0.77 0.29 0.5", "", "", "0 0.5", "0 0.5", "-2 -302", "0 302");
            UI.AddImage(ref container, "Ban.Field", "o.l.3", "0.63 0.77 0.29 0.5", "", "", "0.5 1", "0.5 1", "-150 0", "150 2");
            UI.AddImage(ref container, "Ban.Field", "o.l.4", "0.63 0.77 0.29 0.5", "", "", "0.5 0", "0.5 0", "-150 -2", "150 0");

            UI.AddImage(ref container, "Ban.Field", "Player.Field", "0.141 0.137 0.109 0.8",
                "assets/content/ui/ui.background.tiletex.psd", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "0.5 1", $"0.5 1", $"-150 4", $"150 84");

            UI.AddImage(ref container, "Player.Field", "o.l.1", "0.63 0.77 0.29 0.5", "", "", "1 0.5", "1 0.5", "0 -42", "2 42");
            UI.AddImage(ref container, "Player.Field", "o.l.2", "0.63 0.77 0.29 0.5", "", "", "0 0.5", "0 0.5", "-2 -42", "0 42");
            UI.AddImage(ref container, "Player.Field", "o.l.3", "0.63 0.77 0.29 0.5", "", "", "0.5 1", "0.5 1", "-150 0", "150 2");
            UI.AddImage(ref container, "Player.Field", "o.l.4", "0.63 0.77 0.29 0.5", "", "", "0.5 0", "0.5 0", "-150 -2", "150 0");

            UI.AddRawImage(ref container, "Player.Field", "TargetAvatar", ImageLibrary.Call<string>("GetImage", targetUserID.ToString()), "1 1 1 1", "", "", "0 0.5", "0 0.5", "5 -35", "75 35");
            UI.AddText(ref container, "TargetAvatar", "TargetInfo", "1 1 1 0.65", $"{report.VictimName}", TextAnchor.MiddleLeft, 18, "1 0.5", "1 0.5", "5 5", "300 35");
            UI.AddText(ref container, "TargetAvatar", "TargetInfo2", "1 1 1 0.65", $"[{targetUserID}]", TextAnchor.UpperLeft, 18, "1 0.5", "1 0.5", "5 -35", "300 0");

            UI.AddImage(ref container, "Ban.Field", "BanReasonPanel.BG", "0 0 0 0.5", "", "", "0.5 1", "0.5 1", "-145 -205", "145 -30");
            UI.AddText(ref container, "BanReasonPanel.BG", "ReasonHeader", "1 1 1 0.65", $"ПРИЧИНА БАНА", TextAnchor.MiddleLeft, 18, "0 1", "0 1", "0 2", "300 25");

            UI.AddImage(ref container, "Ban.Field", "BanTimePanel.BG", "0 0 0 0.5", "", "", "0.5 1", "0.5 1", "-145 -435", "145 -230");
            UI.AddText(ref container, "BanTimePanel.BG", "TimeHeader", "1 1 1 0.65", $"ВРЕМЯ БАНА", TextAnchor.MiddleLeft, 18, "0 1", "0 1", "0 2", "300 25");

            //UI.AddButton(ref container, "BanTimePanel.BG", "PermanentBtn", $"permanenttoggle", "", $"0.5 0.5 0.5 0.5", "assets/content/ui/ui.background.transparent.radial.psd", "assets/content/ui/uibackgroundblur.mat", "0.5 0", "0.5 0", "-145 -50", "-115 -20", $"0 0 0 1.00", $"0.5 0.5");
            //UI.AddText(ref container, "PermanentBtn", "PermanentBtn.Status", "0 0.5 0 1", $"{(BanProcess[player.IPlayer].IsPermanent ? "✓" : "")}", TextAnchor.MiddleCenter, 22, "0.5 0.6", "0.5 0.6", "-30 -30", "30 30", "0 0 0 1.00", "1 1", "robotocondensed-regular.ttf");
            //UI.AddText(ref container, "PermanentBtn", "PermanentBtn.Info", "0.7 0.7 0.7 1", $"ПЕРМАНЕНТНЫЙ БАН", TextAnchor.MiddleLeft, 14, "1 0.5", "1 0.5", "5 -16", "140 16");

            //UI.AddButton(ref container, "BanTimePanel.BG", "CurrnetServerBtn", $"allservers", "", $"0.5 0.5 0.5 0.5", "assets/content/ui/ui.background.transparent.radial.psd", "assets/content/ui/uibackgroundblur.mat", "0.5 0", "0.5 0", "-145 -100", "-115 -70", $"0 0 0 1.00", $"0.5 0.5");
            //UI.AddText(ref container, "CurrnetServerBtn", "CurrnetServerBtn.Status", "0 0.5 0 1", $"{(BanProcess[player.IPlayer].AllServers ? "✓" : "")}", TextAnchor.MiddleCenter, 22, "0.5 0.6", "0.5 0.6", "-30 -30", "30 30", "0 0 0 1.00", "1 1", "robotocondensed-regular.ttf");
            //UI.AddText(ref container, "CurrnetServerBtn", "CurrnetServerBtn.Info", "0.7 0.7 0.7 1", $"БАН НА ВСЕХ СЕРВЕРАХ", TextAnchor.MiddleLeft, 14, "1 0.5", "1 0.5", "5 -18", "140 18");

            UI.AddButton(ref container, "Ban.Field", "BanPlayer", $"banplayer", "MainBan.BG", "0.96 0.31 0.26 0.80", "", "", "0.5 0", "0.5 0", "5 10", "135 45");
            UI.AddText(ref container, "BanPlayer", "BanTxt", "1 1 1 0.65", "ЗАБАНИТЬ", TextAnchor.MiddleCenter, 18, "0 0", "1 1", "", "", "0 0 0 1", "0.5 0.5");

            UI.AddButton(ref container, "Ban.Field", "CloseBtn", "", "MainBan.BG", "0.29 0.60 0.83 0.8", "", "", "0.5 0", "0.5 0", "-135 10", "-5 45");
            UI.AddText(ref container, "CloseBtn", "CloseBtnTxt", "1 1 1 0.65", "ЗАКРЫТЬ", TextAnchor.MiddleCenter, 18, "0 0", "1 1", "", "", "0 0 0 1", "0.5 0.5");

            CuiHelper.DestroyUi(player, "MainBan.BG");
            CuiHelper.AddUi(player, container);
            BanReasonPanel(player, "");
            BanTimePanel(player, "");
        }

        [ConsoleCommand("selectreason")]
        void SelectReason_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var reason = string.Join(" ", arg.Args);
            BanProcess[player.IPlayer].BanReason = reason;
            BanReasonPanel(player, reason);
        }

        [ConsoleCommand("selecttime")]
        void SelectTime_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var reason = string.Join("", arg.Args);
            BanProcess[player.IPlayer].BanTime = reason;
            BanTimePanel(player, reason);
        }

        void BanReasonPanel(BasePlayer player, string selectBan)
        {
            var container = new CuiElementContainer();

            var sH = 0.25f;
            var sV = 0.825f;
            var h = sH;
            var v = sV;

            UI.AddImage(ref container, "BanReasonPanel.BG", "BanReasonPanel", "0 0 0 0", "", "", "0 0", "1 1", "", "");

            foreach (var reason in config.BanPressets)
            {
                var olColor = selectBan == reason ? "0.63 0.77 0.29 0.5" : "0.96 0.31 0.26 1.00";

                UI.AddButton(ref container, "BanReasonPanel", "BanButton", $"selectreason {reason}", "", "0.141 0.137 0.109 0.8", "", "", $"{h} {v}", $"{h} {v}", "-60 -15", "60 15");

                UI.AddImage(ref container, "BanButton", "o.l.1", olColor, "", "", "1 0.5", "1 0.5", "0 -17", "2 17");
                UI.AddImage(ref container, "BanButton", "o.l.2", olColor, "", "", "0 0.5", "0 0.5", "-2 -17", "0 17");
                UI.AddImage(ref container, "BanButton", "o.l.3", olColor, "", "", "0.5 1", "0.5 1", "-60 0", "60 2");
                UI.AddImage(ref container, "BanButton", "o.l.4", olColor, "", "", "0.5 0", "0.5 0", "-60 -2", "60 0");

                UI.AddText(ref container, "BanButton", "BanTxt", "1 1 1 0.65", $"{reason}", TextAnchor.MiddleCenter, 18, "0 0", "1 1", "", "");

                v -= 0.225f;
                if (v < 0.1)
                {
                    v = sV;
                    h += 0.5f;
                }
            }

            CuiHelper.DestroyUi(player, "BanReasonPanel");
            CuiHelper.AddUi(player, container);
        }

        void BanTimePanel(BasePlayer player, string selectTime)
        {
            var container = new CuiElementContainer();

            var sH = 0.25f;
            var sV = 0.825f;
            var h = sH;
            var v = sV;

            UI.AddImage(ref container, "BanTimePanel.BG", "BanTimePanel", "0 0 0 0", "", "", "0 0", "1 1", "", "");

            foreach (var reason in config.BanTimes)
            {
                var olColor = selectTime == reason ? "0.63 0.77 0.29 0.5" : "0.96 0.31 0.26 1.00";

                UI.AddButton(ref container, "BanTimePanel", "TimeButton", $"selecttime {reason}", "", "0.141 0.137 0.109 0.8", "", "", $"{h} {v}", $"{h} {v}", "-60 -15", "60 15");

                UI.AddImage(ref container, "TimeButton", "o.l.1", olColor, "", "", "1 0.5", "1 0.5", "0 -17", "2 17");
                UI.AddImage(ref container, "TimeButton", "o.l.2", olColor, "", "", "0 0.5", "0 0.5", "-2 -17", "0 17");
                UI.AddImage(ref container, "TimeButton", "o.l.3", olColor, "", "", "0.5 1", "0.5 1", "-60 0", "60 2");
                UI.AddImage(ref container, "TimeButton", "o.l.4", olColor, "", "", "0.5 0", "0.5 0", "-60 -2", "60 0");

                UI.AddText(ref container, "TimeButton", "BanTxt", "1 1 1 0.65", $"{reason}", TextAnchor.MiddleCenter, 18, "0 0", "1 1", "", "");

                v -= 0.225f;
                if (v < 0.1)
                {
                    v = sV;
                    h += 0.5f;
                }
            }

            CuiHelper.DestroyUi(player, "BanTimePanel");
            CuiHelper.AddUi(player, container);
        }
        [ConsoleCommand("rs.uncheckplayer")]
        void UncheckPlayer_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!permission.UserHasPermission(player.UserIDString, config.ModeratorPermis)) return;
            var targetID = arg.GetUInt64(0);
            UncheckPlayer(player, targetID);

            CuiHelper.DestroyUi(player, "UI_Moderator");
        }


        [ConsoleCommand("banplayer")]
        void BanPlayer(ConsoleSystem.Arg arg)
        {
            var moderator = arg.Player();
            CuiHelper.DestroyUi(moderator, "UI_Moderator");
            if (!permission.UserHasPermission(moderator.UserIDString, config.ModeratorPermis)) return;
            var ban = BanProcess[moderator.IPlayer];
            var victum = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == ban.userID);
            if (victum != null)
            {
                CuiHelper.DestroyUi(victum, "ReportAlert");
                UnityEngine.Object.Destroy(victum.GetComponent<ReportPlayer>());
            }
            else
                UnityEngine.Object.Destroy(BanProcess[moderator.IPlayer].component);

            var report = PlayerReports[ban.userID].Reports.FirstOrDefault(x => x.status == ReportStatys.Process);
            string bancmd = config.BanCommand.Replace($"%STEAMID%", ban.userID.ToString()).Replace($"%REASON%", $"\"{ban.BanReason}\"").Replace("%TIME%", ban.BanTime == "PERMANENT" ? "" : ban.BanTime.ToString());
            rust.RunServerCommand(bancmd);

            Server.Broadcast($"Игрок <color=#f44e42>{report.VictimName}</color> был заблокирован модератором после проверки на зпрещенное ПО");
            SendDiscordMsg($"Игрок {report.VictimName} ({ban.userID}) заблокирован модератором {moderator.displayName} ({moderator.userID}) после проверки на ПО", false);
            SendVKMsg($"Игрок {report.VictimName} ({ban.userID}) заблокирован модератором {moderator.displayName} ({moderator.userID}) после проверки на ПО", config.VKSettings.LogChatID);
            
            if (moderatorData.ContainsKey(moderator.userID))
            {
                moderatorData[moderator.userID].BanCount++;
                moderatorData[moderator.userID].CheckCount++;
            }
            
            var uniqueSenders =
            from n in PlayerReports[ban.userID].Reports.Where(x => x.status == ReportStatys.Process)
            group n by n.SendreID into nGroup
            where nGroup.Count() == 1
            select nGroup.Key; Puts("4");

            ServerMgr.Instance.StartCoroutine(SendRespectToPlayer(uniqueSenders));

            UpdateReportList(ban.userID, ReportStatys.Closed);
        }

        IEnumerator SendRespectToPlayer(IEnumerable<ulong> sortedList)
        {
            foreach (var sendre in sortedList)
            {
                var player = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == sendre);
                if (player != null)
                    SendReply(player, $"Спасибо за вашу активность.\nИгрок на которого Вы отправляли жалобу был забанен на проекте.\nС уважением Администрация.");
                yield return null;
            }
        }

        void UncheckPlayer(BasePlayer moderator, ulong reportedID)
        {
            CuiHelper.DestroyUi(moderator, "UI_Moderator");
            if (!permission.UserHasPermission(moderator.UserIDString, config.ModeratorPermis)) return;
            var victum = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == reportedID);
            if (victum != null)
            {
                var comp = victum.GetComponent<ReportPlayer>();
                UnityEngine.Object.Destroy(comp);
                CuiHelper.DestroyUi(victum, "ReportAlert");
            }
            var report = PlayerReports[reportedID].Reports.FirstOrDefault(x => x.status == ReportStatys.Process);
            Server.Broadcast($"Игрок {report.VictimName} успешно прошел проверку.\nЗапроешенное ПО не обнаружено.");
            SendReply(moderator, $"Вы успешно сняли проверки с игрока {report.VictimName}.");

            SendDiscordMsg($"Модератор {moderator.displayName} ({moderator.userID}) снял проверку с игрока {report.VictimName} ({reportedID}).", false);
            SendVKMsg($"Модератор {moderator.displayName} ({moderator.userID}) снял проверку с игрока {report.VictimName} ({reportedID}).", config.VKSettings.LogChatID);

            UpdateReportList(reportedID, ReportStatys.Closed);

            moderatorData[moderator.userID].CheckCount++;
        }

        void GetUserInfo(ulong userID, string username, Action<bool, string> canUserEnter)
        {
            if (!config.RCCSettings.Enable)
            {
                canUserEnter.Invoke(true, "");
                return;
            }
            if (string.IsNullOrEmpty(config.RCCSettings.APIKey))
            {
                canUserEnter.Invoke(true, "");
                return;
            }

            bool canEnter = true;
            webrequest.Enqueue($"https://rustcheatcheck.ru/panel/api?action=getInfo&key={config.RCCSettings.APIKey}&player={userID}", null, (code, data) =>
            {
                if (code != 200)
                {
                    PrintWarning($"{code}: {data}");
                    canUserEnter.Invoke(canEnter, "");
                    return;
                }
                RCCData rcc = JsonConvert.DeserializeObject<RCCData>(data);
                if (rcc == null)
                {
                    canUserEnter.Invoke(canEnter, "");
                    return;
                }
                if (rcc.status == "error")
                {
                    canUserEnter.Invoke(canEnter, "");
                    return;
                }
                if (rcc.status == "success")
                {
                    if (rcc.bans.Count > 0)
                    {
                        string reason = "";
                        foreach (var info in rcc.bans)
                        {
                            var dTime = new DateTime(1970, 1, 1, 0, 0, 0);
                            var unbanData = new TimeSpan(0, 0, int.Parse(info.unbanDate));
                            var endData = dTime.Add(unbanData);
                            if (unbanData.TotalSeconds > 0 && endData <= DateTime.Now)
                                continue;

                            foreach (var _char in config.RCCSettings.BanReasons)
                            {
                                if (info.reason.ToLower().Contains(_char.ToLower()) && info.active)
                                {
                                    canEnter = false;
                                    reason = info.reason;
                                    break;
                                }
                            }
                        }
                        string reasonAlert = config.RCCSettings.KickPlayers ? $"[RCC]:\nИгрок {username} пытался войти на сервер.\nПопытка входа была заблокирорвана\nПричина: {reason}" : $"[RCC]:\nИгрок {username} заходит на сервер.\nНайдены баны на других сервера.\nПричина: {reason}";
                        if (config.RCCSettings.DSLog)
                            SendDiscordMsg(reasonAlert, false);
                        if (config.RCCSettings.VKLog)
                            SendVKMsg(reasonAlert, config.VKSettings.LogChatID);

                        canUserEnter.Invoke(canEnter, $"You have bans on other Rust projects. {reason}");
                    }
                }

            }, this);
        }

        void SendVKMsg(string msg, string ChatID)
        {
            if (!config.VKSettings.Enable) return;
            if (string.IsNullOrEmpty(ChatID)) return;

            webrequest.Enqueue("https://api.vk.com/method/messages.send?v=5.90", $"&access_token={config.VKSettings.AccesToken}" + $"&random_id={UnityEngine.Random.Range(int.MinValue, int.MaxValue)}" + $"&chat_id={ChatID}" + "&message=" + msg, (code, response) => { }, this, RequestMethod.POST);
        }

        private void SendDiscordMsg(string msg, bool report, Action<int> callback = null)
        {
            if (!config.DiscordSetting.Enable) return;

            if (string.IsNullOrEmpty(config.DiscordSetting.WebHook))
            {
                PrintError($"Репорт не отправлен! Не заполнены поля WebHook");
                return;
            }
            var text = new FancyMessage().AsTTS(false).WithContent(msg);
            webrequest.Enqueue(report ? config.DiscordSetting.WebHook : config.DiscordSetting.LogWebHook, text.ToJson(), (code, response) =>
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
                                var seconds = float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000)
                                    .ToString());
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
            }, this, RequestMethod.POST, new Dictionary<string, string> { ["Content-Type"] = "application/json" });
        }

        public class RCCData
        {
            [JsonProperty("status")]
            public string status;
            [JsonProperty("steamid")]
            public string steamid;
            [JsonProperty("bans")]
            public List<Ban> bans;

            public class Ban
            {
                [JsonProperty("banID")]
                public string banID;
                [JsonProperty("active")]
                public bool active;
                [JsonProperty("reason")]
                public string reason;
                [JsonProperty("serverName")]
                public string serverName;
                [JsonProperty("banDate")]
                public string banDate;
                [JsonProperty("unbanDate")]
                public string unbanDate;
            }
        }

        public class FancyMessage
        {
            [JsonProperty("content")] private string Content { get; set; }

            [JsonProperty("tts")] private bool TextToSpeech { get; set; }

            [JsonProperty("embeds")] private EmbedBuilder[] Embeds { get; set; }

            public FancyMessage WithContent(string value)
            {
                Content = value;
                return this;
            }

            public FancyMessage AsTTS(bool value)
            {
                TextToSpeech = value;
                return this;
            }

            public FancyMessage SetEmbed(EmbedBuilder value)
            {
                Embeds = new[]
                {
                    value
                };
                return this;
            }

            public string GetContent()
            {
                return Content;
            }

            public bool IsTTS()
            {
                return TextToSpeech;
            }

            public EmbedBuilder GetEmbed()
            {
                return Embeds[0];
            }

            public string ToJson()
            {
                var json = new JsonSerializerSettings();
                json.NullValueHandling = NullValueHandling.Ignore;
                return JsonConvert.SerializeObject(this, json);
            }
        }

        public class EmbedBuilder
        {
            public EmbedBuilder()
            {
                Fields = new List<Field>();
            }

            [JsonProperty("title")] private string Title { get; set; }

            [JsonProperty("color")] private int Color { get; set; }

            [JsonProperty("fields")] private List<Field> Fields { get; }

            [JsonProperty("description")] private string Description { get; set; }

            public EmbedBuilder WithTitle(string title)
            {
                Title = title;
                return this;
            }

            public EmbedBuilder WithDescription(string description)
            {
                Description = description;
                return this;
            }

            public EmbedBuilder SetColor(int color)
            {
                Color = color;
                return this;
            }

            public EmbedBuilder SetColor(string color)
            {
                Color = ParseColor(color);
                return this;
            }

            public EmbedBuilder AddInlineField(string name, object value)
            {
                Fields.Add(new Field(name, value, true));
                return this;
            }

            public EmbedBuilder AddField(string name, object value)
            {
                Fields.Add(new Field(name, value, false));
                return this;
            }

            public EmbedBuilder AddField(Field field)
            {
                Fields.Add(field);
                return this;
            }

            public int GetColor()
            {
                return Color;
            }

            public string GetTitle()
            {
                return Title;
            }

            public Field[] GetFields()
            {
                return Fields.ToArray();
            }

            private int ParseColor(string input)
            {
                int color;
                if (!int.TryParse(input, out color)) color = 3329330;
                return color;
            }

            internal class Field
            {
                private readonly object _value;

                public Field(string name, object value, bool inline)
                {
                    Name = name;
                    _value = value;
                    Inline = inline;
                }

                [JsonProperty("name")] public string Name { get; }

                [JsonProperty("value")]
                public object Value => _value;

                [JsonProperty("inline")] public bool Inline { get; }
            }
        }


        public static class UI
        {
            public static void AddImage(ref CuiElementContainer container, string parrent, string name, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax, string outline = "", string dist = "")
            {

                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiImageComponent{Color = color, Sprite = sprite, Material = mat},
                            new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                        }
                    });

                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat) && !string.IsNullOrEmpty(outline))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiImageComponent{ Color = color, Sprite = sprite, Material = mat },
                            new CuiOutlineComponent{Color = outline, Distance = dist},
                            new CuiRectTransformComponent{ AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
                        }
                    });

                if (!string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat) && !string.IsNullOrEmpty(outline))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiImageComponent{ Color = color, Sprite = sprite },
                            new CuiOutlineComponent{Color = outline, Distance = dist},
                            new CuiRectTransformComponent{ AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
                        }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat) && !string.IsNullOrEmpty(outline))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiImageComponent{ Color = color },
                            new CuiOutlineComponent{Color = outline, Distance = dist},
                            new CuiRectTransformComponent{ AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
                        }
                    });

                if (string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiImageComponent{Color = color, Material = mat},
                            new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                        }
                    });

                if (!string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiImageComponent{Color = color, Sprite = sprite},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiImageComponent{Color = color},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
            }

            public static void AddRawImage(ref CuiElementContainer container, string parrent, string name, string png, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax, float FadeIN = 0f, float FadeOut = 0f)
            {
                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        FadeOut = FadeOut,
                        Components =
                    {
                        new CuiRawImageComponent{Color = color, Sprite = sprite, Material = mat, Png = png, FadeIn = FadeIN},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
                if (string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        FadeOut = FadeOut,
                        Components =
                    {
                        new CuiRawImageComponent{Color = color, Material = mat, Png = png, FadeIn = FadeIN},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (!string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        FadeOut = FadeOut,
                        Components =
                    {
                        new CuiRawImageComponent{Color = color, Sprite = sprite, Png = png, FadeIn = FadeIN},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        FadeOut = FadeOut,
                        Components =
                    {
                        new CuiRawImageComponent{Color = color, Png = png, FadeIn = FadeIN},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
            }

            public static void AddText(ref CuiElementContainer container, string parrent, string name, string color, string text, TextAnchor align, int size, string aMin, string aMax, string oMin, string oMax, string outColor = "0 0 0 0", string dist = "1 1", string font = "robotocondensed-bold.ttf", float FadeIN = 0f, float FadeOut = 0f)
            {
                container.Add(new CuiElement()
                {
                    Parent = parrent,
                    Name = name,
                    FadeOut = FadeOut,
                    Components =
                    {
                        new CuiTextComponent{Color = color,Text = text, Align = align, FontSize = size, Font = font, FadeIn = FadeIN},
                        new CuiOutlineComponent{Color = outColor, Distance = dist},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                });

            }

            public static void AddCursor(ref CuiElementContainer container, string parrent)
            {
                container.Add(new CuiElement()
                {
                    Parent = parrent,
                    Components =
                    {
                        new CuiNeedsCursorComponent{ },
                    }
                });
            }

            public static void AddButton(ref CuiElementContainer container, string parrent, string name, string cmd, string close, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax, string outline = "", string dist = "")
            {
                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiButtonComponent{Command = cmd, Color = color, Close = close, Sprite = sprite, Material = mat, },
                            new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                        }
                    });

                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat) && !string.IsNullOrEmpty(outline))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiButtonComponent{Command = cmd, Color = color, Close = close, Sprite = sprite, Material = mat, },
                            new CuiOutlineComponent{Color = outline, Distance = dist},
                            new CuiRectTransformComponent{ AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
                        }
                    });

                if (string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, Material = mat, },
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (!string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, Sprite = sprite},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, },
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });


            }

            public static void AddInputField(ref CuiElementContainer container, string parrent, string name, string cmd, TextAnchor align, int size, int charLimit)
            {
                container.Add(new CuiElement()
                {
                    Parent = parrent,
                    Name = name,
                    Components =
                        {
                            new CuiInputFieldComponent{ Align = align, FontSize = size, Command = cmd, Font = "permanentmarker.ttf", CharsLimit = charLimit },
                            new CuiRectTransformComponent{  AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                });
            }
        }

        public class ReportPlayer : FacepunchBehaviour
        {
            public BasePlayer vicim;
            public Vector3 position;
            public BasePlayer moderator;
            public Vector3 BodyForward;
            float time = ins.config.DsAddTime;


            public bool Run = false;

            public void Init(BasePlayer moderator)
            {
                this.moderator = moderator;
                position = vicim.transform.position;
            }

            void Awake()
            {
                vicim = GetComponent<BasePlayer>();
                BodyForward = vicim.eyes.BodyForward();
                InvokeRepeating(StopMove, 1f, 1f);
            }

            public void StopMove()
            {
                if (vicim == null)
                {
                    CuiHelper.DestroyUi(moderator, "UI_Moderator");
                    Destroy(this);
                    return;
                }
                if (!vicim.IsConnected)
                {
                    ins.SendReply(moderator, $"Player {vicim.displayName} ({vicim.UserIDString}) leave the Game");
                    CuiHelper.DestroyUi(moderator, "UI_Moderator");
                    Destroy(this);
                    return;
                }
                if (vicim.IsWounded())
                {
                    vicim.StopWounded();
                }
                if (vicim.eyes.BodyForward() != BodyForward)
                {
                    BodyForward = vicim.eyes.BodyForward();
                    vicim.lastInputTime = UnityEngine.Time.time;
                }
                
                DrawUIModerator();
            }

            void DrawUIModerator()
            {
                time--;
                var container = new CuiElementContainer();
                UI.AddImage(ref container, "Overlay", "UI_Moderator", "0 0 0 0.5", "", "", "1 0.5", "1 0.5", "-250 0", "0 150");

                var timeLeft = time < 0 ? "<color=red>Время на добавление в Discrod вышло</color>" : $"Времени осталось: <b> {time} сек.</b>";
                var active = vicim.IsConnected ? vicim.IdleTime <= 0f ? "Игрок двигается" : $"Игрок двигался <b>{Math.Floor(vicim.IdleTime)}</b> сек назад." : "Игрок покинул игру";

                UI.AddText(ref container, "UI_Moderator", "InfoText", "1 1 1 1", $"Проверка игрока:\n<b>{vicim.displayName} ({vicim.UserIDString})</b>\n{timeLeft}\nАктивность: \n{active}", TextAnchor.MiddleCenter, 14, "0 0", "1 1", "", "");

                UI.AddButton(ref container, "UI_Moderator", "Ban.BTN", $"rs.banplayer {vicim.userID}", "", "0.341 0.137 0.109 0.8", "assets/content/ui/ui.background.tiletex.psd", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "0 0", "0 0", "0 -35", "120 -5");

                UI.AddText(ref container, "Ban.BTN", "Batn.text", "0.68 0.63 0.60 1", "ЗАБЛОКИРОВАТЬ", TextAnchor.MiddleCenter, 14, "0 0", "1 1", "", "");

                UI.AddButton(ref container, "UI_Moderator", "Ban.BTN", $"rs.uncheckplayer {vicim.userID}", "", "0.28 0.32 0.17 1", "assets/content/ui/ui.background.tiletex.psd", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "1 0", "1 0", "-120 -35", "0 -5");

                UI.AddText(ref container, "Ban.BTN", "Batn.text", "0.68 0.63 0.60 1", "СНЯТЬ ПРОВЕРКУ", TextAnchor.MiddleCenter, 14, "0 0", "1 1", "", "");

                CuiHelper.DestroyUi(moderator, "UI_Moderator");
                CuiHelper.AddUi(moderator, container);
            }

            void OnDestroy()
            {
                Destroy(this);
            }

            public void Disconect()
            {
                ins.SendReply(moderator, $"Игрок {vicim.displayName} ({vicim.UserIDString}) покинул игру");
                ins.SendDiscordMsg($"Игрок {vicim.displayName}[{vicim.userID}] покинул игру во время проверки модератором {moderator.displayName}[{moderator.userID}]", false);
                ins.SendVKMsg($"Игрок {vicim.displayName}[{vicim.userID}] покинул игру во время проверки модератором {moderator.displayName}[{moderator.userID}]", ins.config.VKSettings.LogChatID);
            }
        }

        public class Rootobject
        {
            public Response response { get; set; }
        }

        public class Response
        {
            public int count { get; set; }
            public Item[] items { get; set; }
        }

        public class Item
        {
            public Conversation conversation { get; set; }
            public Last_Message last_message { get; set; }
        }

        public class Conversation
        {
            public Peer peer { get; set; }
            public int last_message_id { get; set; }
            public int in_read { get; set; }
            public int out_read { get; set; }
            public int last_conversation_message_id { get; set; }
            public int in_read_cmid { get; set; }
            public int out_read_cmid { get; set; }
            public bool is_marked_unread { get; set; }
            public bool important { get; set; }
            public Can_Write can_write { get; set; }
            public bool unanswered { get; set; }
            public Push_Settings push_settings { get; set; }
            public Chat_Settings chat_settings { get; set; }
        }

        public class Peer
        {
            public int id { get; set; }
            public string type { get; set; }
            public int local_id { get; set; }
        }

        public class Can_Write
        {
            public bool allowed { get; set; }
            public int reason { get; set; }
        }

        public class Push_Settings
        {
            public bool disabled_forever { get; set; }
            public bool no_sound { get; set; }
            public bool disabled_mentions { get; set; }
            public bool disabled_mass_mentions { get; set; }
        }

        public class Chat_Settings
        {
            public int owner_id { get; set; }
            public string title { get; set; }
            public string state { get; set; }
            public Acl acl { get; set; }
            public int members_count { get; set; }
            public Photo photo { get; set; }
            public int[] admin_ids { get; set; }
            public int[] active_ids { get; set; }
            public bool is_group_channel { get; set; }
            public Permissions permissions { get; set; }
            public bool is_service { get; set; }
        }

        public class Acl
        {
            public bool can_change_info { get; set; }
            public bool can_change_invite_link { get; set; }
            public bool can_change_pin { get; set; }
            public bool can_invite { get; set; }
            public bool can_promote_users { get; set; }
            public bool can_see_invite_link { get; set; }
            public bool can_moderate { get; set; }
            public bool can_copy_chat { get; set; }
            public bool can_call { get; set; }
            public bool can_use_mass_mentions { get; set; }
            public bool can_change_style { get; set; }
        }

        public class Photo
        {
            public string photo_50 { get; set; }
            public string photo_100 { get; set; }
            public string photo_200 { get; set; }
            public bool is_default_photo { get; set; }
            public bool is_default_call_photo { get; set; }
        }

        public class Permissions
        {
            public string invite { get; set; }
            public string change_info { get; set; }
            public string change_pin { get; set; }
            public string use_mass_mentions { get; set; }
            public string see_invite_link { get; set; }
            public string call { get; set; }
            public string change_admins { get; set; }
            public string change_style { get; set; }
        }

        public class Last_Message
        {
            public int date { get; set; }
            public int from_id { get; set; }
            public int id { get; set; }
            public int _out { get; set; }
            public int peer_id { get; set; }
            public string text { get; set; }
            public object[] attachments { get; set; }
            public int conversation_message_id { get; set; }
            public object[] fwd_messages { get; set; }
            public bool important { get; set; }
            public bool is_hidden { get; set; }
            public int random_id { get; set; }
        }


    }
}
