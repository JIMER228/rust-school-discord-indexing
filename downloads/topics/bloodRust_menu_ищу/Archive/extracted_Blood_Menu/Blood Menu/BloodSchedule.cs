using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BloodSchedule", "[LimePlugin] Chibubrik", "3.0.0⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠")]
    public class BloodSchedule : RustPlugin
    {
        #region Вар
        private string Layer = "BloodSchedule_Layer";

        [PluginReference] Plugin ImageLibrary;

        public Calendar calendar = CultureInfo.InvariantCulture.Calendar;
        public enum Types
        {
            None,
            GLOBAL_WIPE,
            WIPE
        }

        Dictionary<int, string> DaysOfWeek = new Dictionary<int, string>()
        {
            [0] = "Понед.",
            [1] = "Вторник",
            [2] = "Среда",
            [3] = "Четверг",
            [4] = "Пятница",
            [5] = "Суббота",
            [6] = "Воскрес.",
        };

        public List<DayClass> DaysList = new List<DayClass>();

        public class DayClass
        {
            public int day;
            public string color;
            public string colorText;
            public Types types;
            public string description;
        }
        #endregion

        #region Конфиг
        private Configuration config;
        private class Configuration
        {
            [JsonProperty("Раз во сколько секунд обновлять календарь?")] public int Delay = 7200;
            [JsonProperty("Описание глобального вайпа")] public string GlobalWipeMessage = "<i><color=#e0947a>Вайп с\nудалением\nизучений в\n13:00 (мск)</color></i>";
            [JsonProperty("Описание обычного вайпа")] public string WipeMessage = "<i><color=#84b4dd>Вайп без\nудаления\nизучений в\n13:00 (мск)</color></i>";
            [JsonProperty("Список дней когда будет вайп (указывается дата, тип вайпа 1 - глобальный вайп, 2 - обычный вайп)")] public Dictionary<string, Types> WipeDays;

            public static Configuration GetNewCong()
            {
                return new Configuration
                {
                    WipeDays = new Dictionary<string, Types> {
                        ["3.1.2025"] = Types.GLOBAL_WIPE,
                        ["10.1.2025"] = Types.WIPE,
                        ["17.1.2025"] = Types.GLOBAL_WIPE,
                        ["24.1.2025"] = Types.WIPE,
                        ["31.1.2025"] = Types.GLOBAL_WIPE,
                        ["7.2.2025"] = Types.WIPE
                    },
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.WipeDays == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Что то с этим конфигом не так! 'oxide/config/{Name}', создаём новую конфигурацию!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewCong();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            CalculateTable();

            ImageLibrary.Call("AddImage", "https://i.imgur.com/8EoXXs4.png", "8EoXXs4");

            timer.Every(config.Delay, () => CalculateTable());
        }
        #endregion

        #region Команды
        [ConsoleCommand("wipe")]
        void CmdConsoleSchedule(ConsoleSystem.Arg args)
        {
            var player = args.Player();

            int index = int.Parse(args.Args[0]);
            var check = DaysList[index];
            float heighText = float.Parse(args.Args[1]);
            if (check.types != Types.None)
            {
                var text = check.types == Types.GLOBAL_WIPE ? config.GlobalWipeMessage : check.types == Types.WIPE ? config.WipeMessage : "";
                var container = new CuiElementContainer();
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 {1 - heighText}" },
                    Text = { Text = $"{text}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                }, Layer + $".Day.Of.{index}", Layer + $".Day.Of.{index}.Text");
                CuiHelper.DestroyUi(player, Layer + $".Day.Of.{index}.Image");
                CuiHelper.DestroyUi(player, Layer + $".Day.Of.{index}.Text");
                CuiHelper.AddUi(player, container);
            }
        }
        #endregion

        #region Интерфейс
        void WipeUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" },
            }, "Menu_Block", Layer);

            float width1 = 0.1325f, height1 = 0.143f, startxBox1 = 0f, startyBox1 = 0.95f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            for (int i = 0; i < DaysList.Count; i++)
            {
                var check = DaysList[i];
                var heigh = i <= 6 ? height1 + 0.05f :  height1;
                var heighText = i <= 6 ? height1 + 0.1f :  0;
                var heighImageMax = i <= 6 ? 0.08f :  0;
                var heighImageMin = i <= 6 ? 0.04f :  0;
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + heigh * 1}", OffsetMax = "0 0" },
                    Button = { Color = check.color, Command = $"wipe {i} {heighText}", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 1f },
                    Text = { Text = "" }
                }, Layer, Layer + $".Day.Of.{i}");
                
                if (check.types != Types.None) {
                    container.Add(new CuiElement
                    {
                        Name = Layer + $".Day.Of.{i}.Image",
                        Parent = Layer + $".Day.Of.{i}",
                        Components = 
                        {
                            new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "8EoXXs4"), Color = check.colorText },
                            new CuiRectTransformComponent { AnchorMin = $"0.8 {0.07 - heighImageMin}", AnchorMax = $"0.96 {0.25 - heighImageMax}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                        }
                    });
                }

                string today = "";
                if (check.color == "1 1 1 0.04")
                    today = DateTime.Now.Day == check.day ? "\nсегодня" : "";

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 {1 - heighText}" },
                    Text = { Text = $"<size=37><b>{check.day}</b></size>{today}", Color = check.colorText, Align = TextAnchor.MiddleCenter, FontSize = 11, Font = "robotocondensed-regular.ttf" }
                }, Layer + $".Day.Of.{i}", Layer + $".Day.Of.{i}.Text");

                xmin1 += width1 + 0.0116f;
                if (xmin1 + width1 >= 1)
                {
                    xmin1 = startxBox1;
                    ymin1 -= height1 + 0.0186f;
                }
            }

            float width = 0.1325f, height = 0.051f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            for (int i = 0; i <= 6; i++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, Layer, "NameDay");
                xmin += width + 0.0116f;

                var check = DaysList[i];
                var color = check.color == HexToCuiColor("#e0947a", 20) ?  HexToCuiColor("#e0947a", 100) : check.color == HexToCuiColor("#84b4dd", 20) ? HexToCuiColor("#e0947a", 100) : check.color == "1 1 1 0.01" ? "1 1 1 0.1" : "1 1 1 0.6";
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = DaysOfWeek[i], Align = TextAnchor.MiddleCenter, FontSize = 12, Color = color, Font = "robotocondensed-bold.ttf" }
                }, "NameDay");
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Utils⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠
        private void CalculateTable()
        {
            DaysList.Clear();
            DateTime date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, calendar);

            var PreviousMonth = date.AddMonths(-1);
            var DaysInPreviousMonth = (int)DateTime.DaysInMonth(PreviousMonth.Year, PreviousMonth.Month);

            int j = Convert.ToInt32(calendar.GetDayOfWeek(date)) - 1;

            j = j == -1 ? 6 : j;

            var LastDay = new DateTime(PreviousMonth.Year, PreviousMonth.Month, DaysInPreviousMonth);
            var backDays = LastDay.AddDays(-j + 1);
            for (int m = 0; m < j; m++)
            {
                DaysList.Add(new DayClass
                {
                    day = backDays.Day,
                    color = "1 1 1 0.01",
                    colorText = "1 1 1 0.1"
                });
                backDays = backDays.AddDays(1);
            }

            int month = calendar.GetMonth(date);
            while (calendar.GetMonth(date) == month)
            {
                var check = config.WipeDays.Where(x => x.Key == $"{date.Day}.{month}.{date.Year}").FirstOrDefault().Value;
                DaysList.Add(new DayClass
                {
                    day = date.Day,
                    color = check == Types.GLOBAL_WIPE ? HexToCuiColor("#e0947a", 20) : check == Types.WIPE ? HexToCuiColor("#84b4dd", 20) : "1 1 1 0.04",
                    colorText = check == Types.GLOBAL_WIPE ? HexToCuiColor("#e0947a", 100) : check == Types.WIPE ? HexToCuiColor("#84b4dd", 100) : "1 1 1 0.6",
                    types = check
                });

                date = date.AddDays(1);
                j--;
            }

            if (DaysList.Count < 42)
            {
                var DaysToEndTable = 42 - DaysList.Count;

                for (int i = 1; i <= DaysToEndTable; i++)
                {
                    var check = config.WipeDays.Where(x => x.Key == $"{i}.{month+1}.{date.Year}").FirstOrDefault().Value;
                    DaysList.Add(new DayClass
                    {
                        day = i,
                        color = check == Types.GLOBAL_WIPE ? HexToCuiColor("#e0947a", 10) : check == Types.WIPE ? HexToCuiColor("#84b4dd", 10) : "1 1 1 0.01",
                        colorText = check == Types.GLOBAL_WIPE ? HexToCuiColor("#e0947a", 20) : check == Types.WIPE ? HexToCuiColor("#84b4dd", 20) : "1 1 1 0.1",
                        types = check
                    });
                }
            }
        }

        public string FirstUpper(string str)
        {
            str = str.ToLower();
            string[] s = str.Split(' ');
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i].Length > 1)
                    s[i] = s[i].Substring(0, 1).ToUpper() + s[i].Substring(1, s[i].Length - 1);
                else s[i] = s[i].ToUpper();
            }
            return string.Join(" ", s);
        }

        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
        }
        #endregion
    }
}