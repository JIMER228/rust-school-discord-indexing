using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FCalandar","Discord: Netrunner#0115","1.0")]
    public class FCalandar : RustPlugin
    {
        #region Fields
        Dictionary<DayOfWeek,int> _weekIndex = new Dictionary<DayOfWeek,int>
        {
            [DayOfWeek.Monday] = 1,
            [DayOfWeek.Tuesday] = 2,
            [DayOfWeek.Thursday] = 4,
            [DayOfWeek.Wednesday] = 3,
            [DayOfWeek.Friday] = 5,
            [DayOfWeek.Saturday] =6,
            [DayOfWeek.Sunday] = 7
        };
        Dictionary<int,DayOfWeek> _dayIndex = new Dictionary<int, DayOfWeek>
        {
            [1] = DayOfWeek.Monday,
            [2] = DayOfWeek.Tuesday,
            [3] = DayOfWeek.Wednesday,
            [4] = DayOfWeek.Thursday,
            [5] = DayOfWeek.Friday,
            [6] = DayOfWeek.Saturday,
            [7] = DayOfWeek.Sunday
        };
        List<int> _wipeDays = new List<int>();
        List<int> _fullDays = new List<int>();
        private int _globalWipe = 0;
        private int _firstWipe = 0;
        private int _monthDays = 0;

        /*enum DayOfWeek
        {
            Monday,
            Tuesday,
            Wednesday,
            Thursday,
            Friday,
            Saturday,
            Sunday 
            
        }*/
        List<string>week =  new List<string>{"Пн","Вт","Cр","Чт","Пт","Сб","Вс"};

            #endregion

        #region Config

        static Configuration config = new Configuration();

        class Configuration
        {
            [JsonProperty("Обычный вайп")] 
            public int Wipe;

            [JsonProperty("Вайп с чертрежами")] public int BluprintWipe;
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    Wipe =  6,
                    BluprintWipe = 2
                };
            }
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) PrintWarning("NULL");
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError($"Не удалось найти конфигурацию 'oxide/config/{Name}', Создание конфига!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data

        

        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            CalcWipes();
        }

        #endregion

        #region Methods

        int GetGlobalWipe()
        {
            DateTime month = new DateTime(DateTime.UtcNow.Year,DateTime.UtcNow.Month,1);
            for (int i = 0; i < 7; i++)
            {
                DateTime thatDay = new DateTime(month.Year, month.Month, month.Day + i);
                
                if (thatDay.DayOfWeek == DayOfWeek.Thursday)
                {
                    PrintWarning($"Global wipe:{thatDay.Day} {thatDay.DayOfWeek}");
                    return thatDay.Day;
                }
            }
            PrintError("Global wipe not found");
            return 0;
        }

        
        void GetWipeDays(int days, int first)
        {
            int counter = first;
            while (counter<=days)
            {
                counter += 7;
                _wipeDays.Add(counter);
            }
        }
        int GetFirstDay()
        {
            DateTime month = new DateTime(DateTime.UtcNow.Year,DateTime.UtcNow.Month,1);
            return _weekIndex[month.DayOfWeek];

        }

        int GetFirstWipe()
        {
            DateTime month = new DateTime(DateTime.UtcNow.Year,DateTime.UtcNow.Month,1);
            for (int i = 0; i < 7; i++)
            {
                DateTime thatDay = new DateTime(month.Year, month.Month, month.Day + i);
                
                if (thatDay.DayOfWeek == _dayIndex[config.Wipe])
                {
                    {
                        PrintWarning($"First wipe:{thatDay.Day} {thatDay.DayOfWeek}");
                        
                        return thatDay.Day;
                    }
                }
            }

            PrintError("First wipe not found");
            return 0;
        }

        void CalcWipes()
        {
            _globalWipe = GetGlobalWipe();
            _firstWipe = GetFirstWipe();
            _wipeDays.Add(_firstWipe);
            GetWipeDays(DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month), _firstWipe);
            GetFullWipes();
        }

        void GetFullWipes()
        {
            if (config.BluprintWipe == 0) return;
            if (config.BluprintWipe == 1)
            {
                _fullDays = _wipeDays;
                return;
            }
            int countwipes = 0;
            foreach (var wipe in _wipeDays)
            {
                countwipes++;
                if (countwipes<config.BluprintWipe) continue;
                _fullDays.Add(wipe);
                countwipes = 0;
            }
        }

        

        string GetColor(int day)
        {
            if (day == _globalWipe)
            {
                return HexToRustFormat("#854d4d");
            }

            if (_fullDays.Contains(day))
            {
                return HexToRustFormat("#4e4d85");
            }
            
            if (_wipeDays.Contains(day))
            {
                return HexToRustFormat("#80854d");
            }

            return HexToRustFormat("#7a7a87");
        }

        #endregion

        #region UI

        private string _layer = "CalendarUI";

        void MainUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-350 -250",OffsetMax = "350 250"},
                Image = {Color = "0 0 0 0"}
            }, "ContentUI", _layer);
            int dayNumber = GetFirstDay();
            double nameOffset = 0,lenght = 100,margin = 5;
            foreach (var dayofweek in week)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{nameOffset+margin} {-75+margin}",OffsetMax = $"{nameOffset+lenght-margin} {0-margin}"},
                    Image = {Color = "0.23 0.23 0.23 1"}
                }, _layer,$"{_layer}{dayNumber}");
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                    Text = {Text = dayofweek,Align = TextAnchor.MiddleCenter,FontSize = 18}
                }, $"{_layer}{dayNumber}");
                nameOffset += lenght;
            }

            int count = 1;
            double startx = lenght*dayNumber-100,starty = -150,height = 75;
            for (int i = 0; i < DateTime.DaysInMonth(DateTime.UtcNow.Year,DateTime.UtcNow.Month); i++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{startx+margin} {starty+margin}",OffsetMax = $"{startx+lenght-margin} {starty+height-margin}"},
                    Image = {Color = GetColor(count)}
                }, _layer,$"{_layer}{count}");
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                    Text = {Text = count.ToString(),Align = TextAnchor.MiddleCenter,FontSize = 18}
                }, $"{_layer}{count}");
                startx += lenght;
                count++;
                dayNumber++;
                if (startx>=700)
                {
                    startx = 0;
                    starty -= height;
                    dayNumber = 0;
                }
            }

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Commands

        [ConsoleCommand("calendar")]
        void ShowCalendar(ConsoleSystem.Arg arg)
        {
            MainUI(arg.Player());
        }

        #endregion
        
        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
    }
}