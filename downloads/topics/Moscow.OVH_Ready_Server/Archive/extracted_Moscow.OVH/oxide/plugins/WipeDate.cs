﻿
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WipeDate", "Hougan", "0.0.1")]
    public class WipeDate : RustPlugin
    {
        #region Variables

        private string Layer = "UI_WipeDateLayer";

        private string ResultJSON = string.Empty;
        private CuiElementContainer ResultContainer = new CuiElementContainer();
        
        
        private Dictionary<DayOfWeek, string> Translations = new Dictionary<DayOfWeek, string>
        {
            [DayOfWeek.Monday] = "Понедельник",
            [DayOfWeek.Tuesday] = "Вторник",
            [DayOfWeek.Wednesday] = "Среда",
            [DayOfWeek.Thursday] = "Четверг",
            [DayOfWeek.Friday] = "Пятница",
            [DayOfWeek.Saturday] = "Суббота",
            [DayOfWeek.Sunday] = "Воскресенье",
        };

        private class Configuration
        {
            public string ServerName = "RED";
            public int YMargin = 0;
            public int MonthID;
            
            public Dictionary<DayOfWeek, Dictionary<int, int>> Dates = new Dictionary<DayOfWeek, Dictionary<int, int>>();
            public Dictionary<DayOfWeek, Dictionary<int, int>> NextDates = new Dictionary<DayOfWeek, Dictionary<int, int>>();

            public static Configuration GenerateDefault()
            {
                return new Configuration
                {
                    MonthID = 0,
                    NextDates = new Dictionary<DayOfWeek, Dictionary<int, int>>
                    {
                        [DayOfWeek.Monday] = new Dictionary<int, int>(),
                        [DayOfWeek.Tuesday] = new Dictionary<int, int>(),
                        [DayOfWeek.Wednesday] = new Dictionary<int, int>(),
                        [DayOfWeek.Thursday] = new Dictionary<int, int>(),
                        [DayOfWeek.Friday] = new Dictionary<int, int>(),
                        [DayOfWeek.Saturday] = new Dictionary<int, int>(),
                        [DayOfWeek.Sunday] = new Dictionary<int, int>(),
                    },
                    Dates = new Dictionary<DayOfWeek, Dictionary<int, int>>
                    {
                        [DayOfWeek.Monday] = new Dictionary<int, int>(),
                        [DayOfWeek.Tuesday] = new Dictionary<int, int>(),
                        [DayOfWeek.Wednesday] = new Dictionary<int, int>(),
                        [DayOfWeek.Thursday] = new Dictionary<int, int>(),
                        [DayOfWeek.Friday] = new Dictionary<int, int>(),
                        [DayOfWeek.Saturday] = new Dictionary<int, int>(),
                        [DayOfWeek.Sunday] = new Dictionary<int, int>(),
                    }
                };
            }
        }
        
        #endregion

        #region Variables

        private static Configuration Settings = null;

        #endregion

        #region Interface
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
                if (Settings == null) LoadDefaultConfig();
            } 
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }
            
            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => Settings = Configuration.GenerateDefault();
        protected override void SaveConfig()        => Config.WriteObject(Settings);

        private void OnServerInitialized()
        {
            DateTime now = DateTime.Now;

            if (Settings.MonthID != now.Month)
            {
                bool currentFull = true;
                
                Settings = Configuration.GenerateDefault();
                Settings.MonthID = now.Month;
                
                for (int i = 1; i <= DateTime.DaysInMonth(now.Year, now.Month); i++)
                {
                    var date = new DateTime(now.Year, now.Month, i);
                    
                    if (!Settings.Dates.ContainsKey(date.DayOfWeek))
                        Settings.Dates.Add(date.DayOfWeek, new Dictionary<int, int>());

                    if (i == 1)
                    {
                        if (date.DayOfWeek == DayOfWeek.Sunday)
                        {
                            for (int t = 0; t < (int) 7; t++) 
                            {
                                if (((DayOfWeek) t) != DayOfWeek.Sunday)
                                    Settings.Dates[(DayOfWeek) t].Add(-1, -1);  
                            }
                        }
                        
                        for (int t = 0; t < (int) date.DayOfWeek; t++)
                        {
                            if (((DayOfWeek) t) != DayOfWeek.Sunday)
                                Settings.Dates[(DayOfWeek) t].Add(-1, -1);  
                        }
                    } 

                    int curWipe = date.DayOfWeek == DayOfWeek.Saturday ? currentFull ? 2 : 1 : 0;
                    Settings.Dates[date.DayOfWeek].Add(i, curWipe);

                    currentFull = curWipe == 0 ? currentFull : !currentFull;
                }

                currentFull = true;
                for (int i = 1; i <= DateTime.DaysInMonth(now.Year, now.Month + 1); i++)
                {
                    var date = new DateTime(now.Year, now.Month + 1, i);
                    
                    if (!Settings.NextDates.ContainsKey(date.DayOfWeek))
                        Settings.NextDates.Add(date.DayOfWeek, new Dictionary<int, int>());

                    if (i == 1)
                    {
                        if (date.DayOfWeek == DayOfWeek.Sunday)
                        {
                            for (int t = 0; t < (int) 7; t++) 
                            {
                                if (((DayOfWeek) t) != DayOfWeek.Sunday)
                                    Settings.NextDates[(DayOfWeek) t].Add(-1, -1);  
                            }
                        }
                        
                        for (int t = 0; t < (int) date.DayOfWeek; t++)
                        {
                            if (((DayOfWeek) t) != DayOfWeek.Sunday)
                                Settings.NextDates[(DayOfWeek) t].Add(-1, -1);  
                        }
                    }

                    int curWipe = date.DayOfWeek == DayOfWeek.Saturday ? currentFull ? 2 : 1 : 0;
                    Settings.NextDates[date.DayOfWeek].Add(i, curWipe);

                    currentFull = curWipe == 0 ? currentFull : !currentFull;
                }

                var max = Settings.Dates.Max(p => p.Value.Count);
                foreach (var check in Settings.Dates)
                {
                    while (check.Value.Count < max)
                        check.Value.Add(Oxide.Core.Random.Range(5, 100) * -1, -1);
                }
                
                max = Settings.NextDates.Max(p => p.Value.Count);
                foreach (var check in Settings.NextDates)
                {
                    while (check.Value.Count < max)
                        check.Value.Add(Oxide.Core.Random.Range(5, 100) * -1, -1);
                }
            }

            ResultContainer.Add(new CuiPanel()
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"-150 {-50 + Settings.YMargin}", OffsetMax = $"0 {70 + Settings.YMargin}"}, 
                Image         = {Color     = "0 0 0 0"}
            }, "UI_RustMenu_Internal", Layer); 
             
            var leftPosition = Settings.Dates.Count / 2f * -120 - (Settings.Dates.Count - 1) / 2f * 5;
            
            ResultContainer.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.5 0.5", AnchorMax   = "0.5 0.5", OffsetMin = $"{leftPosition} 185", OffsetMax         = $"{leftPosition + 500} 300"},
                Text          = {Text      = "РАСПИСАНИЕ ВАЙПОВ", Align = TextAnchor.LowerLeft, Font = "robotocondensed-bold.ttf", FontSize = 38, Color = "0.87 0.87 0.87 1"}
            }, Layer);

            var nextWipeDateinfo = Settings.Dates.SelectMany(p => p.Value).OrderBy(p => p.Key)
                .FirstOrDefault(p => p.Value > 0 && p.Key > DateTime.Now.Day);
            
            int nextWipeDate = Settings.Dates.SelectMany(p => p.Value).OrderBy(p => p.Key).FirstOrDefault(p => p.Value > 0 && p.Key > DateTime.Now.Day).Key;
            if (nextWipeDate == 0)
            {
                nextWipeDate = Settings.NextDates.SelectMany(p => p.Value).OrderBy(p => p.Key).FirstOrDefault(p => p.Value > 0).Key;
            }
            string nextWipeText = $"Следующий вайп <b>{(nextWipeDate.ToString().Length == 1 ? "0" + nextWipeDate.ToString() : nextWipeDateinfo.Key.ToString())}.{(DateTime.Now.Month.ToString().Length == 1 ? "" + (nextWipeDateinfo.Key < (int) DateTime.Now.Day ? ((int) DateTime.Now.Month + 1).ToString() : DateTime.Now.Month.ToString()) : DateTime.Now.Month.ToString())}</b> в <b>13:00</b>";
             
            webrequest.Enqueue($"http://185.200.242.130:2008/wipe/{Settings.ServerName}/{(nextWipeDate.ToString().Length == 1 ? "0" + nextWipeDate.ToString() : nextWipeDateinfo.Key.ToString())}.{(DateTime.Now.Month.ToString().Length == 1 ? "" + (nextWipeDateinfo.Key < (int) DateTime.Now.Day ? ((int) DateTime.Now.Month + 1).ToString() : DateTime.Now.Month.ToString()) : DateTime.Now.Month.ToString())}.{DateTime.Now.Year.ToString().Substring(0, 2)}/{(nextWipeDateinfo.Value == 1 ? "MAP" : "GLOBAL")}/228456ABC", "", (i, s) => { }, this);
            
            ResultContainer.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.5 0.5", AnchorMax   = "0.5 0.5", OffsetMin = $"{leftPosition} 170", OffsetMax         = $"{leftPosition + 500} 300"},
                Text          = {Text      = $"Сегодня <b>{(DateTime.Now.Day.ToString().Length == 1 ? "0" + DateTime.Now.Day.ToString() : DateTime.Now.Day.ToString())}.{(DateTime.Now.Month.ToString().Length == 1 ? "0" + DateTime.Now.Month.ToString() : DateTime.Now.Month.ToString())}</b> | {nextWipeText}", Align = TextAnchor.LowerLeft, Font = "robotocondensed-regular.ttf", FontSize = 18, Color = "0.87 0.87 0.87 1"}
            }, Layer);
             
            ResultContainer.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.5 0.5", AnchorMax   = "0.5 0.5", OffsetMin = $"{leftPosition} 210", OffsetMax         = $"{leftPosition + 125 * 7 - 2} 300"},
                Text          = {Text      = $"ВАЙП КАРТЫ <color=#705050>✖</color>", Align = TextAnchor.LowerRight, Font = "robotocondensed-regular.ttf", FontSize = 15, Color = "0.87 0.87 0.87 1"}
            }, Layer, Layer + ".Remove");
            
            
            ResultContainer.Add(new CuiLabel
            { 
                RectTransform = {AnchorMin = "0.5 0.5", AnchorMax   = "0.5 0.5", OffsetMin = $"{leftPosition} 190", OffsetMax         = $"{leftPosition + 125 * 7 - 2} 300"},
                Text          = {Text      = $"ГЛОБАЛЬНЫЙ ВАЙП <color=#4f5f71>✖</color>", Align = TextAnchor.LowerRight, Font = "robotocondensed-regular.ttf", FontSize = 15, Color = "0.87 0.87 0.87 1"}
            }, Layer, Layer + ".WithoutRemove");
            
            
            ResultContainer.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.5 0.5", AnchorMax   = "0.5 0.5", OffsetMin = $"{leftPosition} 170", OffsetMax         = $"{leftPosition + 125 * 7 - 2} 300"},
                Text          = {Text      = $"СЕГОДНЯШНИЙ ДЕНЬ <color=#b0a69e>✖</color>", Align = TextAnchor.LowerRight, Font = "robotocondensed-regular.ttf", FontSize = 15, Color = "0.87 0.87 0.87 1"}
            }, Layer, Layer + ".Today"); 
            
            
            foreach (var day in Settings.Dates)
            {
                var curName = Translations[day.Key];

                var firstDay = day.Value.FirstOrDefault();
                
                ResultContainer.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = $"0.5 0.5", AnchorMax = $"0.5 0.5", OffsetMin        = $"{leftPosition} 140", OffsetMax        = $"{leftPosition + 120} 160"},
                    Button = { Color = firstDay.Value <= 0 ? firstDay.Key <= -1 ? "0.53 0.51 0.47 0.3" : firstDay.Key == DateTime.Now.Day ? "0.69 0.65 0.62 1" : "0.53 0.51 0.47 0.65" : firstDay.Value == 1 ? "0.52 0.36 0.36 0.8" : "0.36 0.43 0.52 0.8" },
                    Text          = {Text      = curName.ToLower(), Align         = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 16, Color = "1 1 1 0.6"}
                }, Layer, Layer + curName);

                bool firstIterate = true;
                float topPosition = 0;
                foreach (var date in day.Value)
                {
                    if (firstIterate && date.Key > 7)
                        topPosition -= 80;


                    if (date.Key == DateTime.Now.Day && date.Value >= 1) 
                    {
                        
                        
                        ResultContainer.Add(new CuiPanel 
                        {
                            RectTransform = {AnchorMin = $"0 0", AnchorMax = $"1 0", OffsetMin = $"0 {topPosition - 75 + 0}", OffsetMax = $"0 {topPosition -0}"},
                            Image         = {Color     = date.Value <= 0 ? date.Key <= -1 ? "0.53 0.51 0.47 0.3" : date.Key == DateTime.Now.Day ? "0.69 0.65 0.62 1" : "0.53 0.51 0.47 0.65" : date.Value == 1 ? "0.52 0.36 0.36 0.8" : "0.36 0.43 0.52 0.8"}
                        }, Layer + curName, Layer + date.Key);
                        
                        ResultContainer.Add(new CuiPanel
                            {
                                RectTransform = {AnchorMin = $"0 0", AnchorMax = $"1 0", OffsetMin = $"0 0", OffsetMax = $"0 3"},
                                Image         = {Color     = "0.69 0.65 0.62 1"}
                            }, Layer + date.Key);
                        ResultContainer.Add(new CuiPanel
                            {
                                RectTransform = {AnchorMin = $"1 0", AnchorMax = $"1 1", OffsetMin = $"-3 0", OffsetMax = $"0 0"},
                                Image         = {Color     = "0.69 0.65 0.62 1"}
                            }, Layer + date.Key);
                        
                        ResultContainer.Add(new CuiPanel
                            {
                                RectTransform = {AnchorMin = $"0 0", AnchorMax = $"0 1", OffsetMin = $"0 0", OffsetMax = $"3 0"},
                                Image         = {Color     = "0.69 0.65 0.62 1"}
                            }, Layer + date.Key);
                        
                        ResultContainer.Add(new CuiPanel
                            {
                                RectTransform = {AnchorMin = $"0 1", AnchorMax = $"1 1", OffsetMin = $"0 -3", OffsetMax = $"0 0"},
                                Image         = {Color     = "0.69 0.65 0.62 1"}
                            }, Layer + date.Key);
                    }
                    else
                    {
                        ResultContainer.Add(new CuiPanel
                        {
                            RectTransform = {AnchorMin = $"0 0", AnchorMax = $"1 0", OffsetMin = $"0 {topPosition - 75}", OffsetMax = $"0 {topPosition + 0}"},
                            Image         = {Color     = date.Value <= 0 ? date.Key <= -1 ? "0.53 0.51 0.47 0.3" : date.Key == DateTime.Now.Day ? "0.69 0.65 0.62 1" : "0.53 0.51 0.47 0.65" : date.Value == 1 ? "0.52 0.36 0.36 0.8" : "0.36 0.43 0.52 0.8"}
                        }, Layer + curName, Layer + date.Key);
                    }
                    
                    firstIterate = false;
 
                    ResultContainer.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax           = "1 1", OffsetMax              = "0 0"}, 
                        Text          = {Text      = date.Key <= -1 ? "" : date.Key.ToString(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 52, Color = date.Value <= 0 && date.Key == DateTime.Now.Day ? "0.2 0.2 0.2 0.7" : "0.81 0.77 0.74 1"}
                    }, Layer + date.Key, Layer + date.Key + "T");

                    if (date.Value > 0)
                    {
                        ResultContainer.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                            Button        = {Color     = "0 0 0 0", Command = $"UI_WipeDate {date.Value == 2} {date.Key}"},
                            Text          = {Text      = ""}
                        }, Layer + date.Key);
                    }

                    topPosition -= 80;
                }

                leftPosition += 125;
            }

            ResultJSON = CuiHelper.ToJson(ResultContainer);
        }

        #endregion

        #region Commands

        [ConsoleCommand("UI_WipeDate")]
        private void ConsoleHandler(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (!player || !args.HasArgs(1)) return;

            bool fullWipe = bool.Parse(args.Args[0]);
            int icon = int.Parse(args.Args[1]);

            string text = fullWipe ? "ВАЙП С УДАЛЕНИЕМ ИЗУЧЕНИЙ" : "ВАЙП БЕЗ УДАЛЕНИЯ ИЗУЧЕНИЙ";
            CuiHelper.DestroyUi(player, Layer + icon + "T");
            CuiHelper.AddUi(player, new CuiElementContainer
            {
                {
                    new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax                                 = "1 1", OffsetMax              = "0 0"},
                        Text          = {Text      = text, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 14, Color = "0.81 0.77 0.74 1"}
                    },
                    Layer + icon
                }
            });
        }

        [ChatCommand("wipe")]
        private void CmdChatWipe(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, ResultJSON);
        }

        #endregion
    }
}