using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MSNightSkip", "MrSemki", "2.0.0")]
    [Description("Плагин пропуск ночи")]
    public class MSNightSkip : RustPlugin
    {
        #region Config

        private ConfigData config;

        private class ConfigData
        {
            public float NightStartHour = 22f; // когда считаем, что началась ночь
            public float DayStartHour = 8f;    // когда считаем, что начался день
            public float CheckInterval = 5f;   // как часто проверяем переход день/ночь
            public float VoteDuration = 60f;   // секунд
            public float VoteThreshold = 0.6f; // доля онлайна
            public UIBlock UI = new UIBlock();

            public class UIBlock
            {
                // Компактная панель слева от ванильных индикаторов (еда/вода)
                public string AnchorMin = "0.70 0.02";
                public string AnchorMax = "0.84 0.09";
                public int FontSize = 12;
                public string PanelColor = "0 0 0 0.55";
                public string TitleColor = "#FFD700";
                public string CountGood = "#00FF00";
                public string CountTotal = "#FFFFFF";
                public string ButtonColor = "0 0.6 0 0.9";
                public string ButtonTextColor = "#FFFFFF";
                public string WaitingColor = "#AAAAAA";
                public string Title = "Night Vote";
                public string ButtonText = "Skip night";
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null) throw new Exception();
            }
            catch
            {
                PrintWarning("Config file is corrupt or missing, creating a new one...");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        #endregion

        #region State

        private HashSet<ulong> voters = new HashSet<ulong>();
        private bool votingActive;
        private bool isNightNow;
        private Timer voteTimer;

        private const string PanelName = "NightSkipVote.Panel";
        private const string PermShowAlways = "nightvote.showalways";

        #endregion

        #region Hooks

        void Init()
        {
            permission.RegisterPermission(PermShowAlways, this);
        }

        void OnServerInitialized()
        {
            // начальное состояние ночи
            isNightNow = IsNight();

            // периодическая проверка перехода день/ночь
            timer.Every(config.CheckInterval, TickDayNight);

            // показать панель тестерам
            foreach (var p in BasePlayer.activePlayerList)
                TryShowPersistentUI(p);
        }

        void Unload()
        {
            foreach (var p in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(p, PanelName);
        }

        void OnPlayerInit(BasePlayer player)
        {
            TryShowPersistentUI(player);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            CuiHelper.DestroyUi(player, PanelName);
        }

        #endregion

        #region Day/Night detection

        private void TickDayNight()
        {
            var night = IsNight();
            if (!isNightNow && night)
            {
                // Переход от дня к ночи — запускаем голосование
                StartVote();
            }
            isNightNow = night;

            // если голосование активно — обновляем UI
            if (votingActive)
                UpdateUIForAll();
        }

        private bool IsNight()
        {
            var hour = TOD_Sky.Instance.Cycle.Hour;
            // ночь, если час >= NightStartHour ИЛИ час < DayStartHour (учёт перехода через 24:00)
            return hour >= config.NightStartHour || hour < config.DayStartHour;
        }

        #endregion

        #region Voting

        private void StartVote()
        {
            if (votingActive) return;

            votingActive = true;
            voters.Clear();

            PrintToChat($"<color={config.UI.TitleColor}>{config.UI.Title}</color>: Type <color=#ffffff>/votenight</color> or press the button to skip night!");

            foreach (var p in BasePlayer.activePlayerList)
                ShowUI(p);

            voteTimer = timer.Once(config.VoteDuration, EndVote);
        }

        private void EndVote()
        {
            votingActive = false;

            int total = BasePlayer.activePlayerList.Count;
            int voted = voters.Count;
            float percent = total > 0 ? (float)voted / total : 0f;

            if (percent >= config.VoteThreshold)
            {
                PrintToChat($"<color=#00ff00>{voted}/{total}</color> voted. Skipping night!");
                TOD_Sky.Instance.Cycle.Hour = config.DayStartHour;
            }
            else
            {
                PrintToChat($"<color=#ff0000>Not enough votes ({voted}/{total}). Night continues.</color>");
            }

            // Прячем UI всем, кроме тех кто тестирует по пермишену
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (!permission.UserHasPermission(p.UserIDString, PermShowAlways))
                    CuiHelper.DestroyUi(p, PanelName);
                else
                    ShowUI(p); // для тестеров оставим "ожидание"
            }
        }

        [ChatCommand("votenight")]
        private void CmdVoteNight(BasePlayer player, string cmd, string[] args)
        {
            HandleVote(player);
        }

        [ConsoleCommand("nightvote.vote")]
        private void CCmdVoteNight(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            HandleVote(player);
        }

        private void HandleVote(BasePlayer player)
        {
            if (!votingActive)
            {
                player.ChatMessage("There is no active vote.");
                return;
            }

            if (voters.Contains(player.userID))
            {
                player.ChatMessage("You already voted.");
                return;
            }

            voters.Add(player.userID);
            UpdateUIForAll();
        }

        #endregion

        #region UI

        private void TryShowPersistentUI(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, PermShowAlways))
                ShowUI(player);
        }

        private void ShowUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelName);
            CuiHelper.AddUi(player, BuildUI());
        }

        private void UpdateUIForAll()
        {
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (!p.IsConnected) continue;
                if (votingActive || permission.UserHasPermission(p.UserIDString, PermShowAlways))
                    ShowUI(p);
            }
        }

        private CuiElementContainer BuildUI()
        {
            int total = BasePlayer.activePlayerList.Count;
            int voted = voters.Count;

            var ui = new CuiElementContainer();

            // Панель
            ui.Add(new CuiPanel
            {
                Image = { Color = config.UI.PanelColor },
                RectTransform = { AnchorMin = config.UI.AnchorMin, AnchorMax = config.UI.AnchorMax },
                CursorEnabled = false
            }, "Hud", PanelName);

            // Текст
            string content;
            if (votingActive)
            {
                content = $"<color={config.UI.TitleColor}>{config.UI.Title}</color>\n" +
                          $"<color={config.UI.CountGood}>{voted}</color>/<color={config.UI.CountTotal}>{total}</color>";
            }
            else
            {
                content = $"<color={config.UI.TitleColor}>{config.UI.Title}</color>\n" +
                          $"<color={config.UI.WaitingColor}>Waiting...</color>";
            }

            ui.Add(new CuiLabel
            {
                Text = { Text = content, FontSize = config.UI.FontSize, Align = TextAnchor.UpperCenter },
                RectTransform = { AnchorMin = "0.05 0.40", AnchorMax = "0.95 0.95" }
            }, PanelName);

            // Кнопка голосования
            ui.Add(new CuiButton
            {
                Button =
                {
                    Color = config.UI.ButtonColor,
                    Command = "nightvote.vote",
                    FadeIn = 0f
                },
                RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.32" },
                Text =
                {
                    Text = config.UI.ButtonText,
                    FontSize = config.UI.FontSize,
                    Align = TextAnchor.MiddleCenter,
                    Color = config.UI.ButtonTextColor
                }
            }, PanelName);

            return ui;
        }

        #endregion
    }
}
