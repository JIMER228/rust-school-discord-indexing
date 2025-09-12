using Oxide.Game.Rust.Cui;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Newtonsoft.Json;
using System.Collections;
using Oxide.Core.Plugins;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("CalendarController", "Amino", "1.0.2")]
    [Description("A sleek calendar system for Rust")]
    public class CalendarController : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary;

        #region Config
        public static Configuration _config;
        public static UIElements _uiColors;
        Random rnd = new Random();

        public class Configuration
        {
            [JsonProperty("Commands")]
            public List<string> Commands { get; set; } = new List<string>();
            [JsonProperty(PropertyName = "UI Colors (0, 1)")]
            public int UIColors { get; set; } = 0;
            [JsonProperty(PropertyName = "UI Colors 0")]
            public UIElements UIColorsZero { get; set; } = new UIElements();
            [JsonProperty(PropertyName = "UI Colors 1")]
            public UIElements UIColorsOne { get; set; } = new UIElements();
            public List<WeekdayAutoScheduler> WeekdayScheduler = new List<WeekdayAutoScheduler>();
            public List<DayAutoScheduler> DayScheduler = new List<DayAutoScheduler>();
            public List<Legends> Legends { get; set; } = new List<Legends>();
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    Commands = new List<string> { "calendar", "wipe" },
                    UIColorsOne = new UIElements()
                    {
                        MainPanelColor = ".17 .17 .17 1",
                    },
                    Legends = new List<Legends>()
                    {
                        new Legends { LegendName = "Map Wipe", LegendColor = "1 0.38 0.38 .5", LegendDescription = "Map wipe, blueprints not being wiped!", LegendID = 12723},
                        new Legends { LegendName = "Blueprint Wipe", LegendColor = ".36 .64 1 .5", LegendDescription = "Blueprint and map wipes!", LegendID = 126612},
                        new Legends { LegendName = "Special Event", LegendColor = "1 .84 .38 .5", LegendDescription = "Special Events happen!", LegendID = 11267}
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();

                _uiColors = _config.UIColors == 1 ? _config.UIColorsOne : _config.UIColorsZero;
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);
        
        public class WeekdayAutoScheduler
        {
            public int ID { get; set; }
            public int TheDay { get; set; } = 1;
            public List<int> Legends { get; set; } = new List<int>();
            public WeekdayAutoScheduler Clone()
            {
                WeekdayAutoScheduler clone = new WeekdayAutoScheduler();

                clone.ID = this.ID;
                clone.TheDay = this.TheDay;
                clone.Legends = new List<int>(this.Legends);

                return clone;
            }
        }

        public class DayAutoScheduler
        {
            public int ID { get; set; }
            public int EveryXDays { get; set; } = 3;
            public int StartingDay { get; set; } = 1;
            public int DaysSinceAdded { get; set; } = 0;
            public bool FirstDayPosted { get; set; } = false;
            public List<int> Legends { get; set; } = new List<int>();

            public DayAutoScheduler Clone()
            {
                DayAutoScheduler clone = new DayAutoScheduler();

                clone.ID = this.ID;
                clone.EveryXDays = this.EveryXDays;
                clone.StartingDay = this.StartingDay;
                clone.Legends = new List<int>(this.Legends);
                clone.FirstDayPosted = this.FirstDayPosted;
                clone.DaysSinceAdded = this.DaysSinceAdded;

                return clone;
            }
        }

        public class Legends
        {
            public int LegendID { get; set; }
            public string LegendName { get; set; } = string.Empty;
            public string LegendDescription { get; set; } = string.Empty;
            public string LegendColor { get; set; } = "0.17 0.68 1 1";

            public Legends Clone()
            {
                Legends clone = new Legends();

                clone.LegendID = this.LegendID;
                clone.LegendName = this.LegendName;
                clone.LegendDescription = this.LegendDescription;
                clone.LegendColor = this.LegendColor;

                return clone;
            }
        }

        public class UIElements
        {
            public string BlurBackgroundColor { get; set; } = "0 0 0 .4";
            public string MainPanelColor { get; set; } = "0 0 0 0";
            public string TitlePanelColor { get; set; } = "0 0 0 .5";
            public string DefaultDayColor { get; set; } = "0 0 0 .5";
            public string SelectedDayColor { get; set; } = ".32 1 .35 .8";
            public string InvalidDayColor { get; set; } = "0 0 0 .3";
            public string MonthsIndBackgoundColor { get; set; } = "0 0 0 .3";
            public string MonthsIndTitleColor { get; set; } = "0 0 0 .5";
            public string MonthsViewTitleColor { get; set; } = "0 0 0 .4";
            public string DayInfoPanelColor { get; set; } = "0 0 0 .4";
            public string LegendInfoPanelColor { get; set; } = "0 0 0 .4";
            public string DayLegendPanelColor { get; set; } = "0 0 0 .5";
            public string DayLegendNoEventColor { get; set; } = "0 0 0 .7";
            public string HandleColor { get; set; } = "0.15 0.15 0.15 .5";
            public string HighlightColor { get; set; } = "0.17 0.17 0.17 .5";
            public string PressedColor { get; set; } = ".17 .17 .17 .7";
            public string TrackColor { get; set; } = ".09 .09 .09 .4";
        }
        #endregion

        #region Data & Constructors
        public List<MonthData> _monthData = new List<MonthData>();
        public bool usingWC = false;
        public Dictionary<ulong, UserEditData> _userEditData = new Dictionary<ulong, UserEditData>();

        public class UserEditData
        {
            public string SelectionOpen = String.Empty;
            public DayAutoScheduler DayScheduler = new DayAutoScheduler();
            public WeekdayAutoScheduler WeekdayScheduler = new WeekdayAutoScheduler();
            public Legends Legends = new Legends();
            public DayData Day = new DayData();
            public int Year { get; set; }
            public int Month { get; set; }
            public int IntDay { get; set; }
        }

        public class MonthData
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public int FirstDay { get; set; }
            public List<DayData> Days { get; set; } = new List<DayData>();
        }

        public class DayData
        {
            public int Day { get; set; }
            public List<int> Schedules { get; set; } = new List<int>();
            public List<int> Legends { get; set; } = new List<int>();
            public string DaySmallImage { get; set; } = string.Empty;
            public string DayLargeImage { get; set; } = string.Empty;

            public DayData Clone()
            {
                DayData clone = new DayData();

                clone.Day = this.Day;
                clone.Schedules = new List<int>(this.Schedules);
                clone.Legends = new List<int>(this.Legends);
                clone.DaySmallImage = this.DaySmallImage;
                clone.DayLargeImage = this.DayLargeImage;

                return clone;
            }
        }

        public class YMD
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public int Day { get; set; }
        }
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CalendarTitle"] = "CALENDAR",
                ["MonthsTitle"] = "ALL MONTHS",
                ["BackTitle"] = "BACK",
                ["ViewMonth"] = "VIEW MONTH",
                ["NothingHappening"] = "Nothing happening!",
                ["Month_1"] = "January",
                ["Month_2"] = "February",
                ["Month_3"] = "March",
                ["Month_4"] = "April",
                ["Month_5"] = "May",
                ["Month_6"] = "June",
                ["Month_7"] = "July",
                ["Month_8"] = "August",
                ["Month_9"] = "September",
                ["Month_10"] = "October",
                ["Month_11"] = "November",
                ["Month_12"] = "December",
                ["Day_1"] = "Sun",
                ["Day_2"] = "Mon",
                ["Day_3"] = "Tue",
                ["Day_4"] = "Wed",
                ["Day_5"] = "Thu",
                ["Day_6"] = "Fri",
                ["Day_7"] = "Sat",
                ["Sunday"] = "Sunday",
                ["Monday"] = "Monday",
                ["Tuesday"] = "Tuesday",
                ["Wednesday"] = "Wednesday",
                ["Thursday"] = "Thursday",
                ["Friday"] = "Friday",
                ["Saturday"] = "Saturday",
            }, this, "en");
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion

        #region Hooks
        void OnServerInitialized(bool initial)
        {
            var isUsingWC = Interface.Call("IsUsingPlugin", "CalendarController");
            if (isUsingWC != null && (isUsingWC is bool)) usingWC = (bool)isUsingWC;
            else usingWC = false;

            LoadData();
            RegisterCommandsAndPermissions();

            if (ImageLibrary == null || !ImageLibrary.IsLoaded) Puts("ImageLibrary not found, this is needed if you want images on days!");
            else RegisterImages();
        }

        void OnWCRequestedUIPanel(BasePlayer player, string panelName, string neededPlugin)
        {
            if (!neededPlugin.Equals("CalendarController", StringComparison.OrdinalIgnoreCase)) return;
            usingWC = true;
            CMDOpenCalendar(player, null, null);
        }

        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "WelcomeController")
            {
                var isUsingWC = Interface.Call("IsUsingPlugin", "CalendarController");
                if (isUsingWC != null && (isUsingWC is bool)) usingWC = (bool)isUsingWC;
                else usingWC = false;
            }
        }

        void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "WelcomeController")
            {
                usingWC = false;
                RegisterCommandsAndPermissions();
            }
        }

        void OnServerSave()
        {
            SaveData();
        }

        private void Unload()
        {
            if (!Interface.Oxide.IsShuttingDown)
            {
                SaveData();

                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, "CALMainPanel");
                }
            }

            _config = null;
        }
        #endregion

        #region Methods
        void OrderData()
        {
            _monthData = _monthData.OrderBy(x => x.Year).ThenBy(x => x.Month).ToList();
        }

        UserEditData GetOrLoadEditData(BasePlayer player)
        {
            if(!_userEditData.ContainsKey(player.userID)) _userEditData.Add(player.userID, new UserEditData());

            return _userEditData[player.userID];
        }

        void RegisterCommandsAndPermissions()
        {
            if (!usingWC) foreach (var command in _config.Commands)
                    cmd.AddChatCommand(command, this, CMDOpenCalendar);

            permission.RegisterPermission("calendarcontroller.admin", this);
        }

        private void RegisterNewImage(string imageName, string imageUrl) => ImageLibrary?.Call("AddImage", imageUrl, imageName, 0UL);

        private void RegisterImages()
        {
            Dictionary<string, string> imageList = new Dictionary<string, string>();
            foreach (var month in _monthData)
            {
                foreach (var day in month.Days)
                {
                    if(!string.IsNullOrEmpty(day.DaySmallImage)) imageList.Add($"{month.Year}_{month.Month}_{day.Day}_small", day.DaySmallImage);
                    if (!string.IsNullOrEmpty(day.DayLargeImage)) imageList.Add($"{month.Year}_{month.Month}_{day.Day}_large", day.DayLargeImage);
                }
            }

            ImageLibrary?.Call("ImportImageList", "CalendarController", imageList, 0UL, true);
        }

        private YMD GetCurrentMonth()
        {
            var currentDate = DateTime.Now;

            return new YMD { Year = currentDate.Year, Month = currentDate.Month, Day = currentDate.Day };
        }

        public YMD GetNextNeededMonth()
        {
            OrderData();

            var currentDate = DateTime.Now;
            var currentYear = currentDate.Year;
            var currentMonth = currentDate.Month;

            for (int i = 0; i < _monthData.Count; i++)
            {
                var current = _monthData[i];
                var nextExpectedMonth = current.Month == 12 ? 1 : current.Month + 1;
                var nextExpectedYear = current.Month == 12 ? current.Year + 1 : current.Year;

                if (i + 1 < _monthData.Count)
                {
                    var next = _monthData[i + 1];
                    if (next.Year != nextExpectedYear || next.Month != nextExpectedMonth)
                    {
                        return new YMD { Year = nextExpectedYear, Month = nextExpectedMonth };
                    }
                }
                else
                {
                    var nextMonth = current.Month == 12 ? 1 : current.Month + 1;
                    var nextYear = current.Month == 12 ? current.Year + 1 : current.Year;

                    if (current.Year == currentYear && current.Month == currentMonth)
                    {
                        return new YMD { Year = nextYear, Month = nextMonth };
                    }
                }
            }

            var last = _monthData.Last();
            var lastNextMonth = last.Month == 12 ? 1 : last.Month + 1;
            var lastNextYear = last.Month == 12 ? last.Year + 1 : last.Year;

            return new YMD { Year = lastNextYear, Month = lastNextMonth };
        }

        private MonthData GenerateMonth(int year, int month)
        {
            MonthData monthInfo = new MonthData { Year = year, Month = month };

            int monthDays = DateTime.DaysInMonth(year, month);
            DateTime firstDayOfMonth = new DateTime(year, month, 1);

            monthInfo.FirstDay = GetStartDay(firstDayOfMonth.DayOfWeek.ToString());

            for (int day = 1; day <= monthDays; day++)
            {
                monthInfo.Days.Add(new DayData { Day = day });
            }

            return monthInfo;
        }

        private void AddMonthData(MonthData monthData)
        {
            _monthData.Add(monthData);
        }

        private int GetStartDay(string day)
        {
            int startDay = 0;

            switch (day)
            {
                case "Sunday":
                    startDay = 0;
                    break;
                case "Monday":
                    startDay = 1;
                    break;
                case "Tuesday":
                    startDay = 2;
                    break;
                case "Wednesday":
                    startDay = 3;
                    break;
                case "Thursday":
                    startDay = 4;
                    break;
                case "Friday":
                    startDay = 5;
                    break;
                case "Saturday":
                    startDay = 6;
                    break;
            }

            return startDay;
        }

        void UpdateXDayCalendar(int scheduleID)
        {
            DateTime today = DateTime.Today;
            var schedule = _config.DayScheduler.FirstOrDefault(x => x.ID == scheduleID);

            int currentDay = today.Day;

            if (schedule == null) return;
            int months = 0;

            foreach (var month in _monthData)
            {
                if (months > 0) currentDay = 0;
                months++;

                foreach (var day in month.Days)
                {

                    if (!schedule.FirstDayPosted && currentDay <= day.Day)
                    {
                        DateTime dayOfMonth = new DateTime(month.Year, month.Month, day.Day);
                        var dayOfWeek = GetStartDay(dayOfMonth.DayOfWeek.ToString());

                        if (dayOfWeek == schedule.StartingDay - 1)
                        {
                            schedule.FirstDayPosted = true;
                            day.Schedules.Add(schedule.ID);
                        }
                    } else if(schedule.FirstDayPosted)
                    {
                        if (schedule.EveryXDays == schedule.DaysSinceAdded + 1)
                        {
                            schedule.DaysSinceAdded = 0;
                            day.Schedules.Add(schedule.ID);
                        } else schedule.DaysSinceAdded++;
                    }
                }
            }
        }

        void UpdateWeekdayCalendar()
        {
            foreach (var schedule in _config.WeekdayScheduler)
            {
                foreach (var month in _monthData)
                {
                    foreach (var day in month.Days)
                    {
                        if (day.Schedules.Contains(schedule.ID)) continue;

                        DateTime dayOfMonth = new DateTime(month.Year, month.Month, day.Day);
                        var dayOfWeek = GetStartDay(dayOfMonth.DayOfWeek.ToString());

                        if (dayOfWeek != schedule.TheDay - 1) continue;
                        day.Schedules.Add(schedule.ID);
                    }
                }
            }
        }

        void CleanWeekdayCalendar(int ID)
        {
            var theSchedule = _config.WeekdayScheduler.FirstOrDefault(x => x.ID == ID);

            if (theSchedule == null) return;

            foreach (var month in _monthData)
            {
                foreach (var day in month.Days)
                {
                    if (!day.Schedules.Contains(ID)) continue;

                    day.Schedules.Remove(ID);
                }
            }
        }

        void CleanXDayCalendar(int ID)
        {
            var theSchedule = _config.DayScheduler.FirstOrDefault(x => x.ID == ID);

            if (theSchedule == null) return;

            foreach (var month in _monthData)
            {
                foreach (var day in month.Days)
                {
                    if (!day.Schedules.Contains(ID)) continue;

                    day.Schedules.Remove(ID);
                }
            }
        }

        private string GetImage(string imageName)
        {
            if (ImageLibrary == null)
            {
                PrintError("Could not load images due to no Image Library");
                return null;
            }

            return ImageLibrary.Call<string>("GetImage", imageName, 0UL, false);
        }
        #endregion

        #region Commands
        private void CMDOpenCalendar(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            UIOpenCalendar(player);
        }

        [ConsoleCommand("cc_main")]
        private void CMDCalendar(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            switch (arg.Args[0])
            {
                case "close":
                    CuiHelper.DestroyUi(player, "CALMainPanel");
                    break;
                case "select":
                    UIShowDays(player, int.Parse(arg.Args[1]), int.Parse(arg.Args[2]), int.Parse(arg.Args[3]), bool.Parse(arg.Args[4]));
                    break;
                case "admin":
                    UIShowAdminPage(player);
                    break;
                case "editday":
                    var theMonth = _monthData.FindIndex(x => x.Year == int.Parse(arg.Args[1]) && x.Month == int.Parse(arg.Args[2]));
                    MonthData monthData = _monthData[theMonth];

                    int day = int.Parse(arg.Args[3]);

                    var userEdit = GetOrLoadEditData(player);

                    userEdit.Day = monthData.Days.FirstOrDefault(x => x.Day == day).Clone();
                    userEdit.IntDay = day;
                    userEdit.Year = monthData.Year;
                    userEdit.Month = monthData.Month;

                    UICreateAdminDayPanel(player, "CALMainOverlay", monthData.Year, monthData.Month, day, userEdit);
                    break;
                case "months":
                    UIShowAllMonths(player);
                    break;
                case "back":
                    UIOpenCalendar(player);
                    break;
                case "month":
                    theMonth = int.Parse(arg.Args[1]);
                    if (theMonth < 0) theMonth = 0;
                    else if (theMonth > _monthData.Count - 1) theMonth = _monthData.Count - 1;

                    UIOpenCalendar(player, theMonth);
                    break;
                default:
                    CuiHelper.DestroyUi(player, "CALMainPanel");
                    break;
            }
        }

        [ConsoleCommand("cc_edit")]
        private void CMDCalendarEdit(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var userEdit = GetOrLoadEditData(player);

            switch (arg.Args[0])
            {
                case "close":
                    CuiHelper.DestroyUi(player, "CALMainPanel");
                    break;
                case "event":
                    if (!string.IsNullOrEmpty(userEdit.SelectionOpen) && arg.Args[1] == userEdit.SelectionOpen)
                    {
                        CuiHelper.DestroyUi(player, "CCEditDropdown");
                        userEdit.SelectionOpen = string.Empty;
                    }
                    else
                    {
                        userEdit.SelectionOpen = arg.Args[1];
                        UICreateXDayDropdown(player, arg.Args[1], userEdit);
                    }
                    break; 
                case "selectevent":
                    userEdit.SelectionOpen = string.Empty;
                    var theLegend = _config.Legends.FirstOrDefault(x => x.LegendName == String.Join(" ", arg.Args.Skip(2))).Clone();

                    if (arg.Args[1] == "day")
                    {
                        if (userEdit.DayScheduler.Legends.Any(x => x == theLegend.LegendID)) userEdit.DayScheduler.Legends.RemoveAll(x => x == theLegend.LegendID);
                        else userEdit.DayScheduler.Legends.Add(theLegend.LegendID);
                    }
                    else
                    {
                        if (userEdit.WeekdayScheduler.Legends.Any(x => x == theLegend.LegendID)) userEdit.WeekdayScheduler.Legends.RemoveAll(x => x == theLegend.LegendID);
                        else userEdit.WeekdayScheduler.Legends.Add(theLegend.LegendID);
                    }

                    UIShowAdminPage(player);
                    break;
                case "selectday":
                    if (arg.Args[1] == "day")
                    {
                        userEdit.DayScheduler.StartingDay = int.Parse(arg.Args[2]);
                        userEdit.SelectionOpen = string.Empty;
                    }
                    else
                    {
                        userEdit.WeekdayScheduler.TheDay = int.Parse(arg.Args[2]);
                        userEdit.SelectionOpen = string.Empty;
                    }

                    UIShowAdminPage(player);
                    break;
                case "save":
                    if (arg.Args[1] == "day")
                    {
                        var schID = rnd.Next(100, 999999);
                        userEdit.DayScheduler.ID = schID;
                        _config.DayScheduler.Add(userEdit.DayScheduler.Clone());
                        userEdit.DayScheduler = new DayAutoScheduler();

                        UpdateXDayCalendar(schID);
                    }
                    else
                    {
                        userEdit.WeekdayScheduler.ID = rnd.Next(100, 999999);
                        _config.WeekdayScheduler.Add(userEdit.WeekdayScheduler.Clone());
                        userEdit.WeekdayScheduler = new WeekdayAutoScheduler();

                        UpdateWeekdayCalendar();
                    }

                    SaveConfig();
                    UIShowAdminPage(player);
                    break;
                case "schedulexdays":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        if (int.TryParse(arg.Args[1], out int days))
                        {
                            if (days < 1) days = 1;
                            else userEdit.DayScheduler.EveryXDays = days;
                        }
                    }
                    else userEdit.DayScheduler.EveryXDays = 1;

                    UIShowAdminPage(player);
                    break;
                case "addmonth":
                    var neededMonth = GetNextNeededMonth();
                    var theMonth = GenerateMonth(neededMonth.Year, neededMonth.Month);
                    AddMonthData(theMonth);
                    UpdateWeekdayCalendar();

                    OrderData();
                    break;
                case "deletemonth":
                    _monthData.RemoveAt(int.Parse(arg.Args[1]));
                    UIShowAllMonths(player);
                    break;
                case "legends":
                    UIShowAdminPage(player, true);
                    break;
                case "addlegend":
                    if (userEdit.Legends.LegendID == 0)
                    {
                        userEdit.Legends.LegendID = rnd.Next(100, 999999);
                        _config.Legends.Add(userEdit.Legends.Clone());
                    }
                    else
                    {
                        var index = _config.Legends.FindIndex(x => x.LegendID == userEdit.Legends.LegendID);
                        _config.Legends[index] = userEdit.Legends.Clone();
                    }

                    userEdit.Legends = new Legends();

                    UIShowAdminPage(player, true);
                    SaveConfig();
                    break;
                case "legendname":
                    if (arg.Args.Length < 2) userEdit.Legends.LegendName = String.Empty;
                    else userEdit.Legends.LegendName = String.Join(" ", arg.Args.Skip(1));

                    UIShowAdminPage(player, true);
                    break;
                case "legenddesc":
                    if (arg.Args.Length < 2) userEdit.Legends.LegendDescription = String.Empty;
                    else userEdit.Legends.LegendDescription = String.Join(" ", arg.Args.Skip(1));

                    UIShowAdminPage(player, true);
                    break;
                case "legendcolor":
                    if (arg.Args.Length < 2) userEdit.Legends.LegendColor = String.Empty;
                    else userEdit.Legends.LegendColor = String.Join(" ", arg.Args.Skip(1));

                    UIShowAdminPage(player, true);
                    break;
                case "editlegend":
                    userEdit.Legends = _config.Legends[int.Parse(arg.Args[1])].Clone();

                    UIShowAdminPage(player, true);
                    break;
                case "deletelegend":
                    userEdit.Day.Legends.Add(_config.Legends[int.Parse(arg.Args[1])].LegendID);
                    _config.Legends.RemoveAt(int.Parse(arg.Args[1]));

                    UIShowAdminPage(player, true);
                    SaveConfig();
                    break;
                case "selectdayindlegend":
                    int theLeg = _config.Legends[int.Parse(arg.Args[1])].LegendID;

                    if (userEdit.Day.Legends.Any(x => x == theLeg)) userEdit.Day.Legends.RemoveAll(x => x == theLeg);
                    else userEdit.Day.Legends.Add(_config.Legends[int.Parse(arg.Args[1])].LegendID);

                    UICreateAdminDayPanel(player, "CALMainOverlay", userEdit.Year, userEdit.Month, userEdit.IntDay, userEdit);
                    break;
                case "largeimg":
                    if (arg.Args.Length < 2) userEdit.Day.DayLargeImage = String.Empty;
                    else
                    {
                        userEdit.Day.DayLargeImage = String.Join(" ", arg.Args.Skip(1));
                        RegisterNewImage($"{userEdit.Year}_{userEdit.Month}_{userEdit.IntDay}_large", userEdit.Day.DayLargeImage);
                    }

                    timer.Once(.5f, () => UICreateAdminDayPanel(player, "CALMainOverlay", userEdit.Year, userEdit.Month, userEdit.IntDay, userEdit));
                    break;
                case "smallimg":
                    if (arg.Args.Length < 2) userEdit.Day.DaySmallImage = String.Empty;
                    else
                    {
                        userEdit.Day.DaySmallImage = String.Join(" ", arg.Args.Skip(1));
                        RegisterNewImage($"{userEdit.Year}_{userEdit.Month}_{userEdit.IntDay}_small", userEdit.Day.DaySmallImage);
                    }

                    timer.Once(.5f, () => UICreateAdminDayPanel(player, "CALMainOverlay", userEdit.Year, userEdit.Month, userEdit.IntDay, userEdit));
                    break;
                case "saveday":
                    int theMth = _monthData.FindIndex(x => x.Year == int.Parse(arg.Args[1]) && x.Month == int.Parse(arg.Args[2]));
                    var theDay = _monthData[theMth].Days.FindIndex(x => x.Day == int.Parse(arg.Args[3]));

                    _monthData[theMth].Days[theDay] = userEdit.Day.Clone();
                    userEdit.Day = new DayData();

                    UICreateDayPanel(player, "CALMainOverlay", userEdit.Year, userEdit.Month, userEdit.IntDay, _monthData[theMth], true);
                    break;
                case "schedules":
                    UIShowAdminPage(player);
                    break;
                case "deleteday":
                    CleanWeekdayCalendar(int.Parse(arg.Args[1]));

                    _config.WeekdayScheduler.RemoveAt(int.Parse(arg.Args[1]));
                    UpdateWeekdayCalendar();

                    SaveConfig();
                    UIShowAdminPage(player);
                    break;
                case "deletexday":
                    CleanXDayCalendar(int.Parse(arg.Args[1]));

                    _config.DayScheduler.RemoveAt(int.Parse(arg.Args[1]));
                    UpdateWeekdayCalendar();

                    SaveConfig();
                    UIShowAdminPage(player);
                    break;
                default:
                    CuiHelper.DestroyUi(player, "CALMainPanel");
                    break;
            }
        }
        #endregion

        #region UI
        void UIOpenCalendar(BasePlayer player, int index = -1)
        {
            var container = new CuiElementContainer();
            bool isAdmin = permission.UserHasPermission(player.UserIDString, "calendarcontroller.admin");

            CreatePanel(ref container, "0 0", "1 1", usingWC ? "0 0 0 0" : _uiColors.BlurBackgroundColor, usingWC ? "WCSourcePanel" : "Overlay", "CALMainPanel", true, true);

            CreatePanel(ref container, usingWC ? "0 0" : ".15 .1", usingWC ? ".995 1" : ".85 .9", _uiColors.MainPanelColor, "CALMainPanel", "CALOverlayPanel");

            CuiHelper.DestroyUi(player, "CALMainPanel");
            CuiHelper.AddUi(player, container);

            YMD currentDay = null;

            if (index == -1) currentDay = GetCurrentMonth();
            else
            {
                var theMonth = _monthData[index];
                if (theMonth == null) currentDay = GetCurrentMonth();
                else currentDay = new YMD { Year = theMonth.Year, Month = theMonth.Month, Day = 1 };
            }

            Puts(JsonConvert.SerializeObject(currentDay));

            UIShowDays(player, currentDay.Year, currentDay.Month, currentDay.Day, isAdmin);
        }

        void UIShowAdminPage(BasePlayer player, bool isLegends = false)
        {
            var userEdit = GetOrLoadEditData(player);

            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "CALOverlayPanel", "CALMainOverlay");

            CreateButton(ref container, "0 .92", ".624 .995", _uiColors.TitlePanelColor, "1 1 1 1", "BACK", 20, "cc_main back", panel);
            CreateButton(ref container, ".63 .919", ".8125 .995", _uiColors.TitlePanelColor, "1 1 1 1", "ADMIN", 20, "cc_main admin", panel);
            CreateButton(ref container, $".8175 .919", ".999 .995", _uiColors.TitlePanelColor, "1 1 1 1", Lang("MonthsTitle", player.UserIDString), 20, "cc_main months", panel);

            CreateButton(ref container, "0 .84", ".195 .909", "0.24 1 0.3 .5", "0.24 1 0.3 .8", "ADD NEXT MONTH", 20, "cc_edit addmonth", panel);
            CreateButton(ref container, ".0 .76", ".195 .83", "0.17 0.68 1 .5", "0.17 0.68 1 .8", isLegends ? "SCHEDULES": "LEGENDS", 20, $"cc_edit {(isLegends ? "schedules" : "legends")}", panel);

            if (!isLegends)
            {
                UICreateWeekday(player, container, panel, userEdit);
                UICreateXDay(player, container, panel, userEdit);
            } else
            {
                UICreateLegend(player, container, panel, userEdit);
            }

            CuiHelper.DestroyUi(player, "CALMainOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UICreateLegend(BasePlayer player, CuiElementContainer container, string panel, UserEditData userEdit)
        {
            CreateLabel(ref container, ".205 .84", "1 .91", "0 0 0 .5", "1 1 1 1", "Legends Editor", 25, TextAnchor.MiddleCenter, panel);

            var editPanel = CreatePanel(ref container, ".205 .6", "1 .83", "0 0 0 .5", panel);

            CreateLabel(ref container, ".01 .75", ".19 .95", "0 0 0 .5", "1 1 1 1", "LEGEND NAME", 18, TextAnchor.MiddleCenter, editPanel);
            CreateInput(ref container, ".2 .75", ".99 .95", "cc_edit legendname", "0 0 0 .4", "1 1 1 .7", String.IsNullOrEmpty(userEdit.Legends.LegendName) ? " " : userEdit.Legends.LegendName, 15, TextAnchor.MiddleCenter, editPanel);

            CreateLabel(ref container, ".01 .52", ".19 .72", "0 0 0 .5", "1 1 1 1", "LEGEND DESC", 18, TextAnchor.MiddleCenter, editPanel);
            CreateInput(ref container, ".2 .52", ".99 .72", "cc_edit legenddesc", "0 0 0 .4", "1 1 1 .7", String.IsNullOrEmpty(userEdit.Legends.LegendDescription) ? " " : userEdit.Legends.LegendDescription, 15, TextAnchor.MiddleCenter, editPanel);

            CreateLabel(ref container, ".01 .29", ".19 .49", "0 0 0 .5", "1 1 1 1", "LEGEND COLOR", 18, TextAnchor.MiddleCenter, editPanel);
            CreateInput(ref container, ".2 .29", ".99 .49", "cc_edit legendcolor", "0 0 0 .4", "1 1 1 .7", String.IsNullOrEmpty(userEdit.Legends.LegendColor) ? " " : userEdit.Legends.LegendColor, 15, TextAnchor.MiddleCenter, editPanel);

            CreateButton(ref container, ".01 .05", ".99 .26", "0.24 1 0.3 .5", "0.24 1 0.3 .8", "ADD LEGEND", 15, "cc_edit addlegend", editPanel);

            var maxItems = 8;
            var buttonDepth = -.125;
            var LegendCount = _config.Legends.Count;
            AddScrollView(ref container, ".205 0", "1 .59", $"{1 + ((LegendCount < maxItems ? maxItems : LegendCount) * buttonDepth)}", "0 0 0 .5", panel, "CCLegendScroll");

            int i = 0;
            foreach (var legend in _config.Legends)
            {

                var panelDepth = 0 - (buttonDepth * LegendCount);
                var space = LegendCount < maxItems ? .01 : .01 / panelDepth;
                var rowDepth = LegendCount < maxItems ? (-1 * buttonDepth) / 1 : (-1 * buttonDepth) / panelDepth;

                var topHeight = 1 + (rowDepth - ((i + 1) * rowDepth)) - .02;

                var theLegend = _config.Legends[i];

                var pnl = CreatePanel(ref container, $".01 {topHeight - rowDepth + space}", $".98 {topHeight}", "0 0 0 0", "CCLegendScroll", fadeIn: true, fadeInTime: (float)(.3 + (i * .03)));
                CreatePanel(ref container, "0 0", ".02 1", theLegend.LegendColor, pnl);
                CreateLabel(ref container, ".025 0", ".2 1", "0 0 0 .5", "1 1 1 1", theLegend.LegendName, 18, TextAnchor.MiddleCenter, pnl);
                CreateLabel(ref container, ".205 0", ".87 1", "0 0 0 .5", "1 1 1 1", theLegend.LegendDescription, 15, TextAnchor.MiddleCenter, pnl);

                CreateButton(ref container, ".94 0", "1 .965", "0 0 0 .5", "1 1 1 1", "X", 20, $"cc_edit deletelegend {i}", pnl);
                CreateButton(ref container, ".875 0", ".934 .965", "0 0 0 .5", "1 1 1 1", "E", 20, $"cc_edit editlegend {i}", pnl);

                i++;
            }
        }

        void UICreateWeekday(BasePlayer player, CuiElementContainer container, string panel, UserEditData userEdit)
        {
            CreateLabel(ref container, ".205 .84", ".598 .91", "0 0 0 .5", "1 1 1 1", "Weekday Scheduler", 25, TextAnchor.MiddleCenter, panel);

            CreateButton(ref container, ".205 .63", ".598 .67", "0.27 1 0.21 .5", "0.27 1 0.21 .7", "Save Schedule", 15, "cc_edit save weekday", panel);
            CreatePanel(ref container, ".205 .68", ".598 .75", "0 0 0 .5", panel);

            int daySchedules = _config.WeekdayScheduler.Count;
            int maxItemsOnPage = 10;
            double buttonDepth = -.1;
            double panelDepth = daySchedules <= maxItemsOnPage ? 0 : 1 + (buttonDepth * daySchedules);

            AddScrollView(ref container, ".205 .01", ".598 .62", $"{(daySchedules <= maxItemsOnPage ? 0 : panelDepth)}", "0 0 0 .4", panel, "CCWeekdayScheduler");

            panelDepth = daySchedules <= maxItemsOnPage ? 0 : buttonDepth * daySchedules;
            buttonDepth = daySchedules <= maxItemsOnPage ? 0 - buttonDepth : buttonDepth / panelDepth;

            for (int i = 0; i < daySchedules; i++)
            {
                double bottom = 1 - (buttonDepth * (i + 1));
                double top = bottom + buttonDepth;
                double space = daySchedules <= maxItemsOnPage ? .01 : .04 / panelDepth;

                var theDay = _config.WeekdayScheduler[i];
                var legendInfo = theDay.Legends.Count > 0 ? _config.Legends.FirstOrDefault(x => x.LegendID == theDay.Legends[0]) : null;
                var theInfo = CreateLabel(ref container, $".01 {bottom + space}", $".97 {top}", "0 0 0 .4", "1 1 1 1", $"   Every <color=#70ff96>{Lang($"Day_{theDay.TheDay}", player.UserIDString).ToUpper()}</color> put <color=#70ff96>{(theDay.Legends.Count == 0 ? " " : theDay.Legends.Count > 1 ? $"{theDay.Legends.Count} events" : $"{(legendInfo == null ? "COULD NOT FIND" : legendInfo.LegendName)}")}</color>", 15, TextAnchor.MiddleLeft, "CCWeekdayScheduler");
                CreateButton(ref container, ".9 0", "1 1", "0 0 0 0", "1 1 1 1", "X", 20, $"cc_edit deleteday {i}", theInfo);
            }

            var weekdayPanel = CreatePanel(ref container, ".205 .76", ".598 .83", "0 0 0 .5", panel);
            CreateLabel(ref container, "0 0", ".15 1", "0 0 0 0", "1 1 1 1", "Every", 15, TextAnchor.MiddleCenter, weekdayPanel);

            var weekdayDayDropdown = CreatePanel(ref container, ".15 .13", ".5 .85", "0 0 0 .4", weekdayPanel, "CCWeekdayDropdown");
            CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", Lang($"Day_{userEdit.WeekdayScheduler.TheDay}", player.UserIDString), 15, TextAnchor.MiddleCenter, weekdayDayDropdown);
            CreateButton(ref container, ".75 0", "1 1", "0 0 0 0", "1 1 1 1", "+", 15, $"cc_edit event CCWeekdayDropdown", weekdayDayDropdown);

            var weekdayEventDropdown = CreatePanel(ref container, ".52 .13", ".98 .85", "0 0 0 .4", weekdayPanel, "CCWeekdayEventDropdown");
            var legeneds = userEdit.WeekdayScheduler.Legends;
            var legendInfo2 = legeneds.Count > 0 ? _config.Legends.FirstOrDefault(x => x.LegendID == legeneds[0]) : null;
            CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", $"{(legeneds.Count == 0 ? " " : legeneds.Count > 1 ? $"{legeneds.Count} selected" : $"{(legendInfo2 == null ? "COULD NOT FIND" : legendInfo2.LegendName)}")}", 15, TextAnchor.MiddleCenter, weekdayEventDropdown);
            CreateButton(ref container, ".75 0", "1 1", "0 0 0 0", "1 1 1 1", "+", 15, $"cc_edit event CCWeekdayEventDropdown", weekdayEventDropdown);

        }

        void UICreateXDay(BasePlayer player, CuiElementContainer container, string panel, UserEditData userEdit)
        {
            CreateLabel(ref container, ".605 .84", "1 .91", "0 0 0 .5", "1 1 1 1", "X Day Scheduler", 25, TextAnchor.MiddleCenter, panel);

            CreateButton(ref container, ".605 .63", "1 .67", "0.27 1 0.21 .5", "0.27 1 0.21 .7", "Save Schedule", 15, "cc_edit save day", panel);

            int daySchedules = _config.DayScheduler.Count;
            int maxItemsOnPage = 10;
            double buttonDepth = -.1;
            double panelDepth = daySchedules <= maxItemsOnPage ? 0 : 1 + (buttonDepth * daySchedules);

            AddScrollView(ref container, ".605 .01", "1 .62", $"{(daySchedules <= maxItemsOnPage ? 0 : panelDepth)}", "0 0 0 .4", panel, "CCXDayScheduler");

            panelDepth = daySchedules <= maxItemsOnPage ? 0 : buttonDepth * daySchedules;
            buttonDepth = daySchedules <= maxItemsOnPage ? 0 - buttonDepth : buttonDepth / panelDepth;

            for (int i = 0; i < daySchedules; i++)
            {
                double bottom = 1 - (buttonDepth * (i + 1));
                double top = bottom + buttonDepth;
                double space = daySchedules <= maxItemsOnPage ? .01 : .04 / panelDepth;

                var theDay = _config.DayScheduler[i];
                var legendInfo = theDay.Legends.Count > 0 ? _config.Legends.FirstOrDefault(x => x.LegendID == theDay.Legends[0]) : null;
                var theInfo = CreateLabel(ref container, $".01 {bottom + space}", $".97 {top}", "0 0 0 .4", "1 1 1 1", $"   Every {theDay.EveryXDays} day(s) put <color=#70ff96>{(theDay.Legends.Count == 0 ? " " : theDay.Legends.Count > 1 ? $"{theDay.Legends.Count} events" : $"{(legendInfo == null ? "COULD NOT FIND" : legendInfo.LegendName)}")}</color>", 15, TextAnchor.MiddleLeft, "CCXDayScheduler");
                CreateButton(ref container, ".9 0", "1 1", "0 0 0 0", "1 1 1 1", "X", 20, $"cc_edit deletexday {i}", theInfo);
            }

            var xdayPanel2 = CreatePanel(ref container, ".605 .68", "1 .75", "0 0 0 .5", panel);
            var xdayPanel = CreatePanel(ref container, ".605 .76", "1 .83", "0 0 0 .5", panel);
            CreateLabel(ref container, "0 0", ".15 1", "0 0 0 0", "1 1 1 1", "Every", 15, TextAnchor.MiddleCenter, xdayPanel);
            CreateInput(ref container, ".15 .13", ".3 .85", "cc_edit schedulexdays", "0 0 0 .4", "1 1 1 1", $"{userEdit.DayScheduler.EveryXDays}", 20, TextAnchor.MiddleCenter, xdayPanel);
            CreateLabel(ref container, ".3 0", ".45 1", "0 0 0 0", "1 1 1 1", "days", 15, TextAnchor.MiddleCenter, xdayPanel);

            CreateLabel(ref container, "0 0", ".3 1", "0 0 0 0", "1 1 1 1", "starting this", 15, TextAnchor.MiddleCenter, xdayPanel2);
            var xstartDayDropdown = CreatePanel(ref container, ".3 .13", ".98 .85", "0 0 0 .5", xdayPanel2, "CCXDaySelect");
            CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", Lang($"Day_{userEdit.DayScheduler.StartingDay}", player.UserIDString), 15, TextAnchor.MiddleCenter, xstartDayDropdown);
            CreateButton(ref container, ".83 0", "1 1", "0 0 0 0", "1 1 1 1", "+", 15, $"cc_edit event CCXDaySelect", xstartDayDropdown);

            var xdayDayDropdown = CreatePanel(ref container, ".45 .13", ".98 .85", "0 0 0 .5", xdayPanel, "CCXDayDropdown");
            var legeneds = userEdit.DayScheduler.Legends;
            var legendInfo2 = legeneds.Count > 0 ? _config.Legends.FirstOrDefault(x => x.LegendID == legeneds[0]) : null;
            CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", $"{(legeneds.Count == 0 ? " " : legeneds.Count > 1 ? $"{legeneds.Count} selected" : $"{(legendInfo2 == null ? "COULD NOT FIND" : legendInfo2.LegendName)}")}", 15, TextAnchor.MiddleCenter, xdayDayDropdown);
            CreateButton(ref container, ".83 0", "1 1", "0 0 0 0", "1 1 1 1", "+", 15, $"cc_edit event CCXDayDropdown", xdayDayDropdown);
        }

        void UICreateXDayDropdown(BasePlayer player, string panel, UserEditData userEdit)
        {
            var container = new CuiElementContainer();

            int itemAmount = 7;
            double buttonDepth = -.25;

            switch (panel)
            {
                case "CCWeekdayDropdown":
                case "CCXDaySelect":
                    itemAmount = 7;
                    double panelDepth = 1 + (buttonDepth * itemAmount);

                    AddScrollView(ref container, "0 -3.37", "1 -.2", $"{(itemAmount <= 4 ? 0 : panelDepth)}", "0 0 0 .5", panel, "CCEditDropdown");

                    panelDepth = buttonDepth * itemAmount;
                    buttonDepth /= panelDepth;

                    for (int i = 0; i < itemAmount; i++)
                    {
                        double bottom = 1 - (buttonDepth * (i + 1));
                        double top = bottom + buttonDepth;
                        double space = .04 / panelDepth;

                        CreateButton(ref container, $".02 {bottom - space}", $"{(panel == "CCXDaySelect" ? ".95" : ".9")} {top}", "0 0 0 .4", i + 1 == (panel == "CCXDaySelect" ? userEdit.DayScheduler.StartingDay : userEdit.WeekdayScheduler.TheDay) ? "1 0.8 0.23 1" : "1 1 1 1", Lang($"Day_{i + 1}", player.UserIDString), 15, $"cc_edit selectday {(panel == "CCXDaySelect" ? "day" : "weekday")} {i + 1}", "CCEditDropdown");
                    }
                    break;
                case "CCWeekdayEventDropdown":
                case "CCXDayDropdown":
                    itemAmount = _config.Legends.Count;
                    panelDepth = itemAmount <= 4 ? 0 : 1 + (buttonDepth * itemAmount);

                    AddScrollView(ref container, "0 -3.37", "1 -.2", $"{(itemAmount <= 4 ? 0 : panelDepth)}", "0 0 0 .5", panel, "CCEditDropdown");

                    panelDepth = buttonDepth * itemAmount;
                    buttonDepth = itemAmount <= 4 ? 0 - buttonDepth : buttonDepth / panelDepth;


                    for (int i = 0; i < _config.Legends.Count; i++)
                    {
                        double bottom = 1 - (buttonDepth * (i + 1));
                        double top = bottom + buttonDepth;
                        double space = .04 / panelDepth;

                        string textColor = "1 1 1 1";
                        if (panel == "CCXDayDropdown" && userEdit.DayScheduler.Legends.Any(x => x == _config.Legends[i].LegendID)) textColor = "1 0.8 0.23 1";
                        else if(panel == "CCWeekdayEventDropdown" && userEdit.WeekdayScheduler.Legends.Any(x => x == _config.Legends[i].LegendID)) textColor = "1 0.8 0.23 1";

                        CreateButton(ref container, $".02 {bottom - space}", $".93 {top}", "0 0 0 .4", textColor, _config.Legends[i].LegendName, 15, $"cc_edit selectevent {(panel == "CCXDayDropdown" ? "day" : "weekday")} {_config.Legends[i].LegendName}", "CCEditDropdown");
                    }
                    break;
                default:
                    break;
            }

            CuiHelper.DestroyUi(player, "CCEditDropdown");
            CuiHelper.AddUi(player, container);
        }

        void UIShowAllMonths(BasePlayer player)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "CALOverlayPanel", "CALMainOverlay");
            bool isAdmin = permission.UserHasPermission(player.UserIDString, "calendarcontroller.admin");

            CreateLabel(ref container, "0 .92", ".625 .997", _uiColors.TitlePanelColor, "1 1 1 1", $" ", 30, TextAnchor.MiddleCenter, panel);
            if (isAdmin) CreateButton(ref container, ".63 .919", ".8125 .995", _uiColors.TitlePanelColor, "1 1 1 1", "ADMIN", 20, "cc_main admin", panel);
            CreateButton(ref container, $"{(!isAdmin ? .63 : .8175)} .919", ".999 .995", _uiColors.TitlePanelColor, "1 1 1 1", Lang("BackTitle", player.UserIDString), 20, "cc_main back", panel);

            double buttonLength = -.5;
            AddScrollView(ref container, "0 0", "1 .91", $"{(_monthData.Count <= 8 ? 0 : 1 + (Math.Ceiling((double)_monthData.Count / 4) * buttonLength))}", "0 0 0 0", panel, "CCScrollMonths");

            CuiHelper.DestroyUi(player, "CALMainOverlay");
            CuiHelper.AddUi(player, container);

            double row = 0;
            int i = 0;
            foreach (var month in _monthData)
            {
                ServerMgr.Instance.StartCoroutine(UIAddMonth(player, "CCScrollMonths", buttonLength, row, i, isAdmin));
                i++;
                if (i % 4 == 0) row++;
            }
        }

        IEnumerator UIAddMonth(BasePlayer player, string panel, double buttonSize, double row, int i, bool isAdmin)
        {
            var container = new CuiElementContainer();
            var monthCount = _monthData.Count;

            var panelDepth = 0 - (buttonSize * Math.Ceiling((double)monthCount / 4));
            var space = monthCount < 8 ? .01 : .01 / panelDepth;
            var rowDepth = monthCount < 8 ? (-1 * buttonSize) / 1 : (-1 * buttonSize) / panelDepth;

            var topHeight = 1 + (rowDepth - ((row + 1) * rowDepth));

            var theMonth = _monthData[i];

            var pnl = CreatePanel(ref container, $"{0 + (i % 4 * .2525)} {topHeight - rowDepth + space}", $"{.2475 + (i % 4 * .2525)} {topHeight}", "0 0 0 0", panel, fadeIn: true, fadeInTime: (float)(.3 + (i * .03)));
            CreateLabel(ref container, "0 .83", "1 1", _uiColors.MonthsIndTitleColor, "1 1 1 1", $"{Lang($"Month_{theMonth.Month}", player.UserIDString)}, {theMonth.Year}", 15, TextAnchor.MiddleCenter, pnl);
            CreatePanel(ref container, "0 .09", "1 .82", _uiColors.MonthsIndBackgoundColor, pnl);
            CreateButton(ref container, "0 0", ".998 .077", _uiColors.MonthsIndTitleColor, "1 1 1 1", Lang($"ViewMonth", player.UserIDString), 13, $"cc_main select {theMonth.Year} {theMonth.Month} 1 {isAdmin}", pnl);
            if (isAdmin) CreateButton(ref container, ".8 .83", "1 1", "0 0 0 0", "1 1 1 1", "X", 20, $"cc_edit deletemonth {i}", pnl);

            CuiHelper.AddUi(player, container);

            int dayRow = -1;
            for (int ii = 0; ii < 42; ii++)
            {
                if (ii % 7 == 0) dayRow++;
                ServerMgr.Instance.StartCoroutine(UIAddDay(player, theMonth, ii, dayRow, pnl, isAdmin, dayRow, true));
            }
            yield return null;
        }

        void UIShowDays(BasePlayer player, int year, int month, int day, bool isAdmin)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "CALOverlayPanel", "CALMainOverlay");
            var theMonth = _monthData.FindIndex(x => x.Year == year && x.Month == month);
            MonthData monthData = _monthData[theMonth];
            CreateButton(ref container, ".95 .919", "1 .995", _uiColors.TitlePanelColor, "1 1 1 1", !usingWC ? "X" : " ", 30, !usingWC ? "cc_main close" : " ", panel);

            CreateLabel(ref container, ".11 .92", ".625 .997", _uiColors.TitlePanelColor, "1 1 1 1", $"{Lang($"Month_{month}", player.UserIDString)}, {year}", 30, TextAnchor.MiddleCenter, panel);
            if(isAdmin) CreateButton(ref container, ".63 .919", ".7875 .995", _uiColors.TitlePanelColor, "1 1 1 1", "ADMIN", 20, "cc_main admin", panel);
            CreateButton(ref container, $"{(!isAdmin ? .63 : .7925)} .919", ".945 .995", _uiColors.TitlePanelColor, "1 1 1 1", Lang("MonthsTitle", player.UserIDString), 20, "cc_main months", panel);

            CreateButton(ref container, "0 .919", ".05 .995", _uiColors.TitlePanelColor, "1 1 1 1", theMonth <= 0 ? " " : "<", 25, $"cc_main month {theMonth - 1}", panel);
            CreateButton(ref container, ".055 .919", ".105 .995", _uiColors.TitlePanelColor, "1 1 1 1", theMonth >= _monthData.Count - 1 ? " " : ">", 25, $"cc_main month {theMonth + 1}", panel);

            var dayPanel = CreatePanel(ref container, ".63 .054", ".9945 .84", "0 0 0 0", panel, "CALDayPanel");

            var legendPanel = CreatePanel(ref container, "0 0", "1 .048", _uiColors.LegendInfoPanelColor, "CALMainOverlay", "CALLegendPanel");
            double panelLength = .999 / _config.Legends.Count;

            for (int i = 0; i < _config.Legends.Count; i++)
            {
                var theLegend = _config.Legends[i];

                var legendPart = CreatePanel(ref container, $"{i * panelLength} 0", $"{panelLength + (i * panelLength)} 1", "0 0 0 0", legendPanel);
                CreatePanel(ref container, "0 0", ".05 .95", theLegend.LegendColor, legendPart);
                CreateLabel(ref container, ".07 0", "1 1", "0 0 0 0", "1 1 1 1", theLegend.LegendName, 15, TextAnchor.MiddleLeft, legendPart);
            }

            for (int i = 0; i < 7; i++)
            {
                CreateLabel(ref container, $"{0 + (i * .09)} .88", $"{.085 + (i * .09)} .91", _uiColors.DefaultDayColor, "1 1 1 1", Lang($"Day_{i+1}", player.UserIDString), 13, TextAnchor.MiddleCenter, panel);
            }

            CuiHelper.DestroyUi(player, "CALMainOverlay");
            CuiHelper.AddUi(player, container);

            UICreateDayPanel(player, panel, year, month, day, monthData, isAdmin);

            int row = -1;
            for (int i = 0; i < 42; i++)
            {
                if (i % 7 == 0) row++;
                ServerMgr.Instance.StartCoroutine(UIAddDay(player, monthData, i, row, "CALMainOverlay", isAdmin, day));
            }
        }

        void UICreateAdminDayPanel(BasePlayer player, string panel, int year, int month, int day, UserEditData userEdit)
        {
            var container = new CuiElementContainer();

            var dayPanel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "CALDayPanel", "CALDayPanelOverlay");

            CreateLabel(ref container, "0 .95", "1 1", "0 0 0 .5", "1 1 1 1", "LEGENDS", 15, TextAnchor.MiddleCenter, dayPanel);

            var maxItems = 4;
            var buttonDepth = -.25;
            var LegendCount = _config.Legends.Count;
            AddScrollView(ref container, "0 .73", "1 .94", $"{1 + ((LegendCount < maxItems ? maxItems : LegendCount) * buttonDepth)}", "0 0 0 .4", dayPanel, "CCLegendScroll");

            int i = 0;
            foreach (var legend in _config.Legends)
            {

                var panelDepth = 0 - (buttonDepth * LegendCount);
                var space = LegendCount < maxItems ? .05 : .05 / panelDepth;
                var rowDepth = LegendCount < maxItems ? (-1 * buttonDepth) / 1 : (-1 * buttonDepth) / panelDepth;

                var topHeight = 1 + (rowDepth - ((i + 1) * rowDepth)) - .02;

                var theLegend = _config.Legends[i];
                bool isUsing = userEdit.Day.Legends.Any(x => x == legend.LegendID);

                var pnl = CreatePanel(ref container, $".01 {topHeight - rowDepth + space}", $".98 {topHeight}", "0 0 0 0", "CCLegendScroll", fadeIn: true, fadeInTime: (float)(.3 + (i * .03)));
                CreatePanel(ref container, "0 0", ".02 1", theLegend.LegendColor, pnl);
                CreateLabel(ref container, ".025 0", "1 1", "0 0 0 .5", isUsing ? "1 0.8 0.23 1" : "1 1 1 1", theLegend.LegendName, 18, TextAnchor.MiddleCenter, pnl);

                CreateButton(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", " ", 20, $"cc_edit selectdayindlegend {i}", pnl);

                i++;
            }

            CreateLabel(ref container, "0 .67", ".7 .72", "0 0 0 .5", "1 1 1 1", "LARGE IMG", 15, TextAnchor.MiddleCenter, dayPanel);
            CreateLabel(ref container, ".71 .67", "1 .72", "0 0 0 .5", "1 1 1 1", "SMALL IMG", 15, TextAnchor.MiddleCenter, dayPanel);

            CreateInput(ref container, "0 .58", ".7 .66", "cc_edit largeimg", "0 0 0 .4", "1 1 1 1", userEdit.Day.DayLargeImage, 15, TextAnchor.MiddleCenter, dayPanel);
            CreateInput(ref container, ".71 .58", "1 .66", "cc_edit smallimg", "0 0 0 .4", "1 1 1 1", userEdit.Day.DaySmallImage, 15, TextAnchor.MiddleCenter, dayPanel);

            var largeImg = CreatePanel(ref container, "0 .07", ".7 .57", "0 0 0 .4", dayPanel);
            var smallImg = CreatePanel(ref container, ".71 .36", "1 .57", "0 0 0 .4", dayPanel);

            CreateImagePanel(ref container, "0 0", "1 1", string.IsNullOrEmpty(userEdit.Day.DayLargeImage) ? null : GetImage($"{userEdit.Year}_{userEdit.Month}_{userEdit.IntDay}_large"), largeImg);
            CreateImagePanel(ref container, "0 0", "1 1", string.IsNullOrEmpty(userEdit.Day.DaySmallImage) ? null : GetImage($"{userEdit.Year}_{userEdit.Month}_{userEdit.IntDay}_small"), smallImg);

            CreateButton(ref container, "0 0", "1 .06", "0.24 1 0.3 .5", "0.24 1 0.3 .8", "SAVE DAY", 15, $"cc_edit saveday {year} {month} {day}", dayPanel);

            CuiHelper.DestroyUi(player, "CALDayPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UICreateDayPanel(BasePlayer player, string panel, int year, int month, int day, MonthData monthData, bool isAdmin)
        {
            var container = new CuiElementContainer();

            var dayPanel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "CALDayPanel", "CALDayPanelOverlay");

            var neededDate = new DateTime(year, month, day);
            var labelPanel = CreateLabel(ref container, ".63 .85", "1 .91", _uiColors.DayInfoPanelColor, "1 1 1 1", $"{Lang($"{neededDate.DayOfWeek}", player.UserIDString)}, {Lang($"Month_{neededDate.Month}", player.UserIDString)} {day}, {year}", 20, TextAnchor.MiddleCenter, panel);
            if(isAdmin) CreateButton(ref container, ".85 0", "1 1", "0 0 0 0", "1 0.23 0.25 .6", "EDIT", 15, $"cc_main editday {year} {month} {day}", labelPanel);

            var theDay = monthData.Days.FirstOrDefault(x => x.Day == day);

            List<Legends> legends = new List<Legends>();

            foreach (var schedule in theDay.Schedules)
            {

                var theSchedule = _config.WeekdayScheduler.FirstOrDefault(x => x.ID == schedule);
                var xDaySchedule = _config.DayScheduler.FirstOrDefault(x => x.ID == schedule);

                if (theSchedule == null && xDaySchedule == null) continue;

                if (theSchedule != null)
                {
                    foreach (var legend in theSchedule.Legends)
                    {
                        if (legends.Any(x => x.LegendID == legend)) continue;

                        var theEvent = _config.Legends.FirstOrDefault(x => x.LegendID == legend);

                        if (theEvent == null) continue;
                        legends.Add(theEvent);
                    }
                }

                if (xDaySchedule != null)
                {
                    foreach (var legend in xDaySchedule.Legends)
                    {
                        if (legends.Any(x => x.LegendID == legend)) continue;

                        var theEvent = _config.Legends.FirstOrDefault(x => x.LegendID == legend);

                        if (theEvent == null) continue;
                        legends.Add(theEvent);
                    }
                }
            }

            foreach (var legend in theDay.Legends)
            {
                var theEvent = _config.Legends.FirstOrDefault(x => x.LegendID == legend);

                if (theEvent == null) continue;
                if (legends.Any(x => x.LegendID == theEvent.LegendID)) continue;

                legends.Add(theEvent);
            }


            if (legends.Count == 0)
            {
                CreatePanel(ref container, $"0 .885", $".03 .995", _uiColors.DayLegendNoEventColor, dayPanel);
                CreateLabel(ref container, $".032 .885", $"1.01 .995", _uiColors.DayLegendPanelColor, "1 1 1 1", Lang("NothingHappening", player.UserIDString), 15, TextAnchor.MiddleCenter, dayPanel);
            } else for (int i = 0; i < legends.Count; i++)
            {
                CreatePanel(ref container, $"0 {.995 - .115 - (i * .125)}", $".03 {.995 - (i * .125)}", legends[i].LegendColor, dayPanel);
                CreateLabel(ref container, $".032 {.995 - .115 - (i * .125)}", $"1.01 {.995 - (i * .125)}", _uiColors.DayLegendPanelColor, "1 1 1 1", legends[i].LegendDescription, 15, TextAnchor.MiddleCenter, dayPanel);
            }

            if (!string.IsNullOrEmpty(theDay.DayLargeImage)) CreateImagePanel(ref container, "0 0", ".56 .4", GetImage($"{year}_{month}_{theDay.Day}_large"), dayPanel);

            CuiHelper.DestroyUi(player, "CALDayPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        IEnumerator UIAddDay(BasePlayer player, MonthData monthData, int day, int row, string panel, bool isAdmin, int selectedDay, bool isAll = false)
        {
            var container = new CuiElementContainer();

            var isDay = day >= monthData.FirstDay && (day - (monthData.FirstDay - 1)) <= monthData.Days.Count;
            var spc = isAll ? .144 : .09;
            var upSpc = isAll ? .122 : .137;
            var topPos = isAll ? .82 : .87;
            var dayPanel = CreatePanel(ref container, $"{0 + (day % 7 * spc)} {topPos - (isAll ? .115 : .13) - (row * upSpc)}", $"{(isAll ? .134 : .085) + (day % 7 * spc)} {topPos - (row * upSpc)}", isDay ? day - monthData.FirstDay + 1 == selectedDay ? _uiColors.SelectedDayColor : _uiColors.DefaultDayColor : _uiColors.InvalidDayColor, panel, fadeIn: true, fadeInTime: (float)(.3 + (.03 * day)));

            if (isDay)
            {
                var theDay = monthData.Days[day - monthData.FirstDay];
                if (!string.IsNullOrEmpty(theDay.DaySmallImage)) CreateImagePanel(ref container, "0 0", ".99 1", GetImage($"{monthData.Year}_{monthData.Month}_{day - monthData.FirstDay + 1}_small"), dayPanel);

                List<Legends> legends = new List<Legends>();

                foreach (var schedule in theDay.Schedules)
                {
                    var theSchedule = _config.WeekdayScheduler.FirstOrDefault(x => x.ID == schedule);
                    var xDaySchedule = _config.DayScheduler.FirstOrDefault(x => x.ID == schedule);

                    if (theSchedule == null && xDaySchedule == null) continue;

                    if (theSchedule != null)
                    {
                        foreach (var legend in theSchedule.Legends)
                        {
                            if (legends.Any(x => x.LegendID == legend)) continue;

                            var theEvent = _config.Legends.FirstOrDefault(x => x.LegendID == legend);

                            if (theEvent == null) continue;
                            legends.Add(theEvent);
                        }
                    }

                    if (xDaySchedule != null)
                    {
                        foreach (var legend in xDaySchedule.Legends)
                        {
                            if (legends.Any(x => x.LegendID == legend)) continue;

                            var theEvent = _config.Legends.FirstOrDefault(x => x.LegendID == legend);

                            if (theEvent == null) continue;
                            legends.Add(theEvent);
                        }
                    }
                }

                foreach (var legend in theDay.Legends)
                {
                    var theEvent = _config.Legends.FirstOrDefault(x => x.LegendID == legend);

                    if (theEvent == null) continue;
                    if (legends.Any(x => x.LegendID == theEvent.LegendID)) continue;

                    legends.Add(theEvent);
                }

                double panelLength = legends.Count == 1 ? .99 : .999 / legends.Count;

                for (int i = 0; i < legends.Count; i++)
                {
                    var top = .995 - (i * panelLength);
                    CreatePanel(ref container, $"0 {top - panelLength + .005}", $".99 {top}", legends[i].LegendColor, dayPanel);
                }
            }

            if (isDay)
            {
                CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", $"{day - monthData.FirstDay + 1}", isAll ? 15 : 20, TextAnchor.MiddleCenter, dayPanel);
                CreateButton(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", " ", 15, $"cc_main select {monthData.Year} {monthData.Month} {day - monthData.FirstDay + 1} {isAdmin}", dayPanel);
            }

            CuiHelper.AddUi(player, container);

            yield return null;
        }
        #endregion

        #region Data Methods
        private void LoadData()
        {
            var calendar = Interface.GetMod().DataFileSystem.ReadObject<List<MonthData>>("CalendarController");
            YMD currentMonth = GetCurrentMonth();

            if (calendar == null || calendar.Count == 0)
            {
                calendar = new List<MonthData>();

                _monthData = calendar;

                MonthData monthData = GenerateMonth(currentMonth.Year, currentMonth.Month);
                AddMonthData(monthData);
            } else _monthData = calendar;

            if (_monthData.FirstOrDefault(x => x.Year == currentMonth.Year && x.Month == currentMonth.Month) == null)
            {
                MonthData monthData = GenerateMonth(currentMonth.Year, currentMonth.Month);
                AddMonthData(monthData);
            }

            foreach (var month in _monthData)
            {
                foreach (var day in month.Days)
                {
                    List<int> scheduleForRemove = new List<int>();
                    foreach (var schedule in day.Schedules)
                    {
                        if (_config.WeekdayScheduler.Any(x => x.ID == schedule)) continue;
                        if (_config.DayScheduler.Any(x => x.ID == schedule)) continue;

                        scheduleForRemove.Add(schedule);
                    }

                    foreach (var schedule in scheduleForRemove) day.Schedules.Remove(schedule);
                }
            }

            OrderData();
        }

        private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("CalendarController", _monthData);
        #endregion

        #region UI Methods
        static public void AddScrollView(ref CuiElementContainer container, string anchorMin, string anchorMax, string offSetMin, string color, string parent = "Overlay", string panelName = null, bool horizontal = false)
        {
            container.Add(new CuiElement
            {
                Name = panelName,
                Parent = parent,
                Components = {
                        new CuiImageComponent {
                            FadeIn = 0.2f,
                            Color = color
                        },
                        new CuiScrollViewComponent {
                            Horizontal = false,
                            Vertical = true,
                            MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                            Elasticity = 0.25f,
                            Inertia = true,
                            DecelerationRate = 0.3f,
                            ContentTransform = new CuiRectTransform()
                            {
                                AnchorMin = $"0 {(horizontal ? "0" : offSetMin)}",
                                AnchorMax = $"{(horizontal ? offSetMin : "1")} 1",
                            },
                            ScrollSensitivity = 30.0f,
                            VerticalScrollbar = new CuiScrollbar {
                                Invert = false,
                                AutoHide = false,
                                HandleSprite = "assets/content/ui/ui.rounded.tga",
                                HandleColor = _uiColors.HandleColor,
                                HighlightColor = _uiColors.HighlightColor,
                                TrackSprite = "assets/content/ui/ui.background.tile.psd",
                                TrackColor = _uiColors.TrackColor,
                                Size = 6,
                                PressedColor = _uiColors.PressedColor
                            },
                            HorizontalScrollbar = new CuiScrollbar {
                                Invert = true,
                                AutoHide = false,
                                HandleSprite = "assets/content/ui/ui.rounded.tga",
                                HandleColor = _uiColors.HandleColor,
                                HighlightColor = _uiColors.HighlightColor,
                                TrackSprite = "assets/content/ui/ui.background.tile.psd",
                                TrackColor = _uiColors.TrackColor,
                                Size = 6,
                                PressedColor = _uiColors.PressedColor
                            }
                        },
                        new CuiRectTransformComponent {AnchorMin = anchorMin, AnchorMax = anchorMax}
                    }
            });
        }

        private static string CreateItemPanel(ref CuiElementContainer container, string anchorMin, string anchorMax, float padding, string color, int itemId, string parent = "Overlay", string panelName = null, ulong skinId = 0L)
        {
            var panel = container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },
                Image = { Color = color }
            }, parent, panelName);

            container.Add(new CuiElement
            {
                Parent = panel,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{padding} {padding + .004f}",
                        AnchorMax = $"{1 - padding - .004f} {1 - padding - .02f}"
                    },
                    new CuiImageComponent {ItemId = itemId, SkinId = skinId}
                }
            });

            return panel;
        }

        private static string CreateLabel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string textColor, string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay", string panelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, panelColor, parent, panelName);
            container.Add(new CuiLabel
            {
                Text =
                {
                    Color = textColor,
                    Text = labelText,
                    Align = alignment,
                    FontSize = fontSize,
                    Font = "robotocondensed-bold.ttf"
                }
            }, panel);
            return panel;
        }

        private static string CreatePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string parent = "Overlay", string panelName = null, bool blur = false, bool isMainPanel = false, string offsetMin = null, string offsetMax = null, bool fadeIn = false, float fadeInTime = 1)
        {
            CuiPanel panel = new CuiPanel
            {
                RectTransform =
            {
                AnchorMin = anchorMin,
                AnchorMax = anchorMax
            },
                Image = { Color = panelColor }
            };

            if (offsetMax != null) panel.RectTransform.OffsetMax = offsetMax;
            if (offsetMax != null) panel.RectTransform.OffsetMin = offsetMin;
            if (fadeIn) panel.Image.FadeIn = fadeInTime;

            if (blur) panel.Image.Material = "assets/content/ui/uibackgroundblur.mat";
            if (isMainPanel) panel.CursorEnabled = true;
            return container.Add(panel, parent, panelName);
        }

        private static void CreateImagePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelImage, string parent = "Overlay", string panelName = null, bool isUrl = false)
        {
            var panel = new CuiElement
            {
                Parent = parent,
                Name = panelName,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    }
                }
            };

            if (isUrl) panel.Components.Add(new CuiRawImageComponent { Url = panelImage });
            else panel.Components.Add(new CuiRawImageComponent { Png = panelImage });

            container.Add(panel);
        }

        private static void CreateImageButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string buttonCommand, string panelImage, string parent = "Overlay", string panelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, buttonColor, parent, panelName);
            CreateImagePanel(ref container, "0 0", "1 1", panelImage, panel);

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"{buttonCommand}" }
            }, panel);
        }

        private static string CreateButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string textColor, string buttonText, int fontSize, string buttonCommand, string parent = "Overlay", TextAnchor labelAnchor = TextAnchor.MiddleCenter)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, "0 0 0 0", parent);

            container.Add(new CuiButton
            {
                Button = { Color = buttonColor, Command = $"{buttonCommand}" },
                Text = { Align = labelAnchor, Color = textColor, FontSize = fontSize, Text = buttonText },
            }, panel);
            return panel;
        }

        private static string CreateInput(ref CuiElementContainer container, string anchorMin, string anchorMax, string command, string backgroundColor, string textColor, string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay", string labelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, backgroundColor, parent, labelName);

            container.Add(new CuiElement
            {
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Color = textColor,
                        Text = labelText,
                        Align = alignment,
                        FontSize = fontSize,
                        Font = "robotocondensed-bold.ttf",
                        NeedsKeyboard = true,
                        Command = command
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                },
                Parent = panel
            });

            return panel;
        }
        #endregion
    }
}
