using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("EventsManager Converter", "Mevent", "1.0.2")]
    public class EventsManagerConverter : RustPlugin
    {
        #region V2 Data Structures
        private class TimeEntriesData
        {
            [JsonProperty(PropertyName = "Time Entries")]
            public Dictionary<string, EventSettingsEntry> Entries = new();
        }

        private class EventSettingsEntry
        {
            public string GUID;
            public string Name;
            public bool StaticTime = true;
            public int RandomOffsetMinuteMin;
            public int RandomOffsetMinuteMax;
            public int Hour = 12;
            public int Minute = 30;
            public int MinPlayers;
            public Dictionary<int, bool> DaysActive = new()
            {
                [1] = false,
                [2] = false,
                [3] = false,
                [4] = false,
                [5] = false,
                [6] = false,
                [0] = false
            };
            public List<string> RandomStartEvents = new();
        }

        private class EventSettings
        {
            [JsonProperty(PropertyName = "Event name")]
            public string displayName = "Air Event";
            [JsonProperty(PropertyName = "The command to launch the event")]
            public string Command = "airevent start";
            [JsonProperty(PropertyName = "Color")]
            public string Color = "0.1 0.1 0.1 0.95";
            [JsonProperty("Command type")]
            public string CommandType;
        }
        #endregion

        #region V3 Data Structures
        private class Event
        {
            public string displayName = "Enter event name...";
            public string pluginName = "Enter event plugin name...";
            public string command = "Enter command...";
            public string creator = "Choose event creator...";
            public int eventId;
        }

        private class EventTime
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public EventType type = EventType.Static;
            public HashSet<DayOfWeek> activeDays = new HashSet<DayOfWeek>();
            public int hour;
            public int minute;
            public int interval;
            public int offsetMin;
            public int offsetMax;
            public int minPlayers;
            public List<int> eventsId = new List<int>();
        }

        private enum EventType
        {
            Static,
            Every,
            Random
        }
        #endregion

        #region Commands

        [ConsoleCommand("eventmanager.convert.v2")]
        private void CmdConvertData(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;

            try
            {
                var nextEventId = 0;

                var v2Events = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<EventSettings>>>("EventManager/events");
                var v2TimeEntries = Interface.Oxide.DataFileSystem.ReadObject<TimeEntriesData>("EventManager/TimeEntries");
                if (v2Events == null || v2TimeEntries == null)
                {
                    SendReply(arg, "Error: V2 data files are missing.");
                    return;
                }

                var v3Events = new List<Event>();
                var v3EventTimes = new List<EventTime>();
                var nameToEventId = new Dictionary<string, int>();
                var creators = new HashSet<string>();

                foreach (var entry in v2Events)
                {
                    var parts = entry.Key.Split('|');
                    if (parts.Length != 3)
                    {
                        Puts($"Invalid key format: {entry.Key}");
                        continue;
                    }
                    var authorName = parts[0];
                    creators.Add(authorName);

                    foreach (var eventSettings in entry.Value)
                    {
                        string pluginName = "";

                        if (authorName == "Facepunch") pluginName = "Facepunch";

                        if (eventSettings.displayName.Contains("Defendable-Bases"))
                            pluginName = "DefendableBases";

                        else if (eventSettings.displayName.Contains("GuardedCrate"))
                            pluginName = "GuardedCrate";

                        else if (eventSettings.displayName.Contains("RaidableBases"))
                            pluginName = "RaidableBases";

                        else if (eventSettings.displayName.Contains("Meteor-Event"))
                            pluginName = "MeteorEvent";

                        else
                        {
                            var @string = Regex.Replace(eventSettings.displayName, @"[^\w\^0-9a-zA-Z]", "");
                            pluginName = @string;
                        }

                        var ev = new Event
                        {
                            displayName = eventSettings.displayName,
                            pluginName = pluginName,
                            command = eventSettings.Command,
                            creator = authorName,
                            eventId = nextEventId++
                        };
                        v3Events.Add(ev);
                        nameToEventId[eventSettings.displayName] = ev.eventId;
                    }
                }

                foreach (var timeEntry in v2TimeEntries.Entries.Values)
                {

                    EventType eventType;

                    if (timeEntry.StaticTime)
                        eventType = EventType.Static;
                    else
                        eventType = EventType.Random;


                    if (timeEntry.Name == "RANDOM START")
                    {
                        var eventsToStart = new List<int>();

                        foreach (var randomEvent in timeEntry.RandomStartEvents)
                        {
                            if (nameToEventId.TryGetValue(randomEvent, out var randomEventID))
                            {
                                eventsToStart.Add(randomEventID);
                            }
                        }

                        var et = new EventTime
                        {
                            type = eventType,
                            hour = timeEntry.Hour,
                            minute = timeEntry.Minute,
                            interval = 0,
                            offsetMin = eventType == EventType.Random ? (timeEntry.Hour * 60 + timeEntry.Minute) + timeEntry.RandomOffsetMinuteMin : timeEntry.RandomOffsetMinuteMin,
                            offsetMax = eventType == EventType.Random ? (timeEntry.Hour * 60 + timeEntry.Minute) + timeEntry.RandomOffsetMinuteMax : timeEntry.RandomOffsetMinuteMax,
                            minPlayers = timeEntry.MinPlayers,
                            activeDays = new HashSet<DayOfWeek>(timeEntry.DaysActive
                            .Where(kv => kv.Value)
                            .Select(kv => (DayOfWeek)kv.Key)),
                            eventsId = eventsToStart
                        };
                        v3EventTimes.Add(et);
                    }
                    else
                    {
                        if (!nameToEventId.TryGetValue(timeEntry.Name, out int eventId))
                        {
                            Puts($"Warning: No event found for Name '{timeEntry.Name}' in time entry");
                            continue;
                        }

                        var et = new EventTime
                        {
                            type = eventType,
                            hour = timeEntry.Hour,
                            minute = timeEntry.Minute,
                            interval = 0,
                            offsetMin = eventType == EventType.Random ? (timeEntry.Hour * 60 + timeEntry.Minute) : timeEntry.RandomOffsetMinuteMin,
                            offsetMax = eventType == EventType.Random ? (timeEntry.Hour * 60 + timeEntry.Minute) : timeEntry.RandomOffsetMinuteMax,
                            minPlayers = timeEntry.MinPlayers,
                            activeDays = new HashSet<DayOfWeek>(timeEntry.DaysActive
                            .Where(kv => kv.Value)
                            .Select(kv => (DayOfWeek)kv.Key)),
                            eventsId = new List<int> { eventId }
                        };
                        v3EventTimes.Add(et);
                    }
                }

                Interface.Oxide.DataFileSystem.WriteObject("EventManager/CreatorsSettings", creators.ToList());
                Interface.Oxide.DataFileSystem.WriteObject("EventManager/EventsSettings", v3Events);
                Interface.Oxide.DataFileSystem.WriteObject("EventManager/TimesSettings", v3EventTimes);

                Interface.Oxide.ReloadPlugin("EventsManager");

                Puts($"Conversion completed: {creators.Count} creators, {v3Events.Count} events, {v3EventTimes.Count} event times.");
            }
            catch (Exception ex)
            {
                Puts($"Conversion failed: {ex.Message}");
            }
        }

        #endregion
    }
}