using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("EclipseManager", "Автор", "1.1.1")]
    [Description("Плагин для управления затмениями с GUI меню и авто-повтором")]
    public class EclipseManager : RustPlugin
    {
        #region Configuration
        
        private static Configuration config;
        
        public class Configuration
        {
            [JsonProperty("Восходное затмение")]
            public EclipseSettings SunriseEclipse { get; set; } = new EclipseSettings
            {
                Year = 2020,
                Month = 6,
                Day = 21,
                Time = 7
            };

            [JsonProperty("Закатное затмение")]
            public EclipseSettings SunsetEclipse { get; set; } = new EclipseSettings
            {
                Year = 2001,
                Month = 12,
                Day = 14,
                Time = 19
            };

            [JsonProperty("Интервал авто-повтора (секунды)")]
            public float AutoRepeatInterval { get; set; } = 3600f;
        }
        
        public class EclipseSettings
        {
            [JsonProperty("Год")]
            public int Year { get; set; }
            
            [JsonProperty("Месяц")]
            public int Month { get; set; }
            
            [JsonProperty("День")]
            public int Day { get; set; }
            
            [JsonProperty("Время")]
            public int Time { get; set; }
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }
        
        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }
        
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        
        #endregion

        #region Переменные
        
        private const string GUI_NAME = "EclipseManagerUI";
        private Timer _autoCheckTimer;
        private bool _autoSunriseEnabled = false;
        private bool _autoSunsetEnabled = false;
        private int _lastSunriseDay = -1;
        private int _lastSunsetDay = -1;
        
        #endregion

        #region GUI
        
        private void CreateUI(BasePlayer player)
        {
            DestroyUI(player);
            
            var elements = new CuiElementContainer();
            
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.95" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-200 -250", OffsetMax = "200 250" },
                CursorEnabled = true
            }, "Hud", GUI_NAME);
            
            elements.Add(new CuiLabel
            {
                Text = { Text = "Управление затмениями", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 1" }
            }, GUI_NAME, "Header");
            
            elements.Add(new CuiButton
            {
                Button = { Command = "eclipse.sunrise", Color = "0.3 0.5 0.8 1" },
                RectTransform = { AnchorMin = "0.1 0.8", AnchorMax = "0.9 0.85" },
                Text = { Text = "Затмение на восходе", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, GUI_NAME, "SunriseBtn");
            
            elements.Add(new CuiButton
            {
                Button = { Command = "eclipse.sunset", Color = "0.8 0.5 0.3 1" },
                RectTransform = { AnchorMin = "0.1 0.7", AnchorMax = "0.9 0.75" },
                Text = { Text = "Затмение на закате", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, GUI_NAME, "SunsetBtn");
            
            elements.Add(new CuiButton
            {
                Button = { Command = _autoSunriseEnabled ? "eclipse.autosunriseoff" : "eclipse.autosunriseon", 
                          Color = _autoSunriseEnabled ? "0.2 0.8 0.2 1" : "0.8 0.2 0.2 1" },
                RectTransform = { AnchorMin = "0.1 0.6", AnchorMax = "0.9 0.65" },
                Text = { Text = _autoSunriseEnabled ? "Авто-восход: ВКЛ" : "Авто-восход: ВЫКЛ", 
                        FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, GUI_NAME, "AutoSunriseBtn");
            
            elements.Add(new CuiButton
            {
                Button = { Command = _autoSunsetEnabled ? "eclipse.autosunsetoff" : "eclipse.autosunseton", 
                         Color = _autoSunsetEnabled ? "0.2 0.8 0.2 1" : "0.8 0.2 0.2 1" },
                RectTransform = { AnchorMin = "0.1 0.5", AnchorMax = "0.9 0.55" },
                Text = { Text = _autoSunsetEnabled ? "Авто-закат: ВКЛ" : "Авто-закат: ВЫКЛ", 
                       FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, GUI_NAME, "AutoSunsetBtn");
            
            elements.Add(new CuiButton
            {
                Button = { Command = "eclipse.reset", Color = "0.5 0.5 0.5 1" },
                RectTransform = { AnchorMin = "0.1 0.4", AnchorMax = "0.9 0.45" },
                Text = { Text = "Сбросить время", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, GUI_NAME, "ResetBtn");
            
            elements.Add(new CuiButton
            {
                Button = { Command = "eclipse.closeui", Color = "0.8 0.2 0.2 1" },
                RectTransform = { AnchorMin = "0.1 0.3", AnchorMax = "0.9 0.35" },
                Text = { Text = "Закрыть", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, GUI_NAME, "CloseBtn");
            
            CuiHelper.AddUi(player, elements);
        }
        
        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, GUI_NAME);
        }
        
        #endregion

        #region Команды
        
        [ConsoleCommand("eclipse.menu")]
        private void CmdEclipseMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            CreateUI(player);
        }
        
        [ConsoleCommand("eclipse.sunrise")]
        private void CmdSunriseEclipse(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            SetEclipse(config.SunriseEclipse);
            player.ChatMessage("Установлено восходное затмение!");
        }
        
        [ConsoleCommand("eclipse.sunset")]
        private void CmdSunsetEclipse(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            SetEclipse(config.SunsetEclipse);
            player.ChatMessage("Установлено закатное затмение!");
        }
        
        [ConsoleCommand("eclipse.autosunriseon")]
        private void CmdAutoSunriseOn(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            _autoSunriseEnabled = true;
            _autoSunsetEnabled = false;
            _lastSunriseDay = -1;
            _lastSunsetDay = -1;
            StartAutoCheckTimer();

            player.ChatMessage("Авто-восходное затмение включено!");
            if (player.IsConnected) CreateUI(player);
        }
        
        [ConsoleCommand("eclipse.autosunriseoff")]
        private void CmdAutoSunriseOff(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            _autoSunriseEnabled = false;
            StopAutoCheckTimerIfNeeded();
            player.ChatMessage("Авто-восходное затмение выключено!");
            if (player.IsConnected) CreateUI(player);
        }
        
        [ConsoleCommand("eclipse.autosunseton")]
        private void CmdAutoSunsetOn(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            _autoSunsetEnabled = true;
            _autoSunriseEnabled = false;
            _lastSunriseDay = -1;
            _lastSunsetDay = -1;
            StartAutoCheckTimer();

            player.ChatMessage("Авто-закатное затмение включено!");
            if (player.IsConnected) CreateUI(player);
        }
        
        [ConsoleCommand("eclipse.autosunsetoff")]
        private void CmdAutoSunsetOff(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            _autoSunsetEnabled = false;
            StopAutoCheckTimerIfNeeded();
            player.ChatMessage("Авто-закатное затмение выключено!");
            if (player.IsConnected) CreateUI(player);
        }
        
        [ConsoleCommand("eclipse.reset")]
        private void CmdResetEclipse(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            ResetTime();
            player.ChatMessage("Время сброшено!");
        }
        
        [ConsoleCommand("eclipse.closeui")]
        private void CmdCloseUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            DestroyUI(player);
        }
        
        #endregion

        #region Функции
        
        private void SetEclipse(EclipseSettings settings)
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "env.year", settings.Year.ToString());
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "env.month", settings.Month.ToString());
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "env.day", settings.Day.ToString());
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "env.time", settings.Time.ToString());
        }
        
        private void ResetTime()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "env.resettime");
        }
        
        private void StartAutoCheckTimer()
        {
            if (_autoCheckTimer != null) return;
            _autoCheckTimer = timer.Every(10f, CheckEclipseTime);
        }

        private void StopAutoCheckTimerIfNeeded()
        {
            if (!_autoSunriseEnabled && !_autoSunsetEnabled)
            {
                _autoCheckTimer?.Destroy();
                _autoCheckTimer = null;
            }
        }

        private void CheckEclipseTime()
        {
            float currentTime = TOD_Sky.Instance.Cycle.Hour;
            int currentDay = TOD_Sky.Instance.Cycle.Day;

            if (_autoSunriseEnabled)
            {
                if (currentDay != _lastSunriseDay && Mathf.FloorToInt(currentTime) == config.SunriseEclipse.Time)
                {
                    SetEclipse(config.SunriseEclipse);
                    Puts($"Автоматически установлено восходное затмение (день {currentDay}, время {currentTime})");
                    _lastSunriseDay = currentDay;
                }
            }
            if (_autoSunsetEnabled)
            {
                if (currentDay != _lastSunsetDay && Mathf.FloorToInt(currentTime) == config.SunsetEclipse.Time)
                {
                    SetEclipse(config.SunsetEclipse);
                    Puts($"Автоматически установлено закатное затмение (день {currentDay}, время {currentTime})");
                    _lastSunsetDay = currentDay;
                }
            }
        }
        
        #endregion

        #region Хуки
        
        [ChatCommand("eclipse")]
        private void EclipseCommand(BasePlayer player, string command, string[] args)
        {
            CreateUI(player);
        }
        
        void Unload()
        {
            _autoCheckTimer?.Destroy();
            _autoCheckTimer = null;
        }
        
        #endregion
    }
}