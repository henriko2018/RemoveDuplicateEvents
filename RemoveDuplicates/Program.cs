using CommandLine;
using console_csharp_connect_sample;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace RemoveDuplicates
{
    internal static class Program
    {
        private static Options _options;

        [STAThread]
        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts =>
                {
                    _options = opts;
                    if (_options.KeepLongestBody && _options.KeepRecurring)
                        throw new ArgumentException("You can't use both --keepLongestBody and --keepRecurring.");
                    RunOptionsAndReturnExitCodeAsync().GetAwaiter().GetResult();
                });

            if (Debugger.IsAttached)
            {
                Console.Out.WriteLine();
                Console.Out.Write("Press any key to exit.");
                Console.ReadKey();
            }
        }

        private static async Task RunOptionsAndReturnExitCodeAsync()
        {
            try
            {
                var graphClient = AuthenticationHelper.GetAuthenticatedClient();
                if (graphClient != null)
                {
                    WriteInfo("# Calendars:");
                    var calendars = await graphClient.Me.Calendars.Request().GetAsync();
                    foreach (var calendar in calendars)
                    {
                        WriteInfo($"- {calendar.Name}");
                    }

                    if (string.IsNullOrEmpty(_options.Calendar))
                    {
                        WriteInfo("Use -h or --help to view options.");
                    }
                    else
                    {
                        var calendar = calendars.SingleOrDefault(c => c.Name.ToLower() == _options.Calendar.ToLower());
                        if (calendar != null)
                            await ProcessCalendarAsync(graphClient, calendar);
                        else
                            WriteError($"Invalid calendar name \"{_options.Calendar}\".");
                    }
                }
                else
                {
                    WriteError(
                        "We weren't able to create a GraphServiceClient for you. Please check the output for errors.");
                }
            }
            catch (Exception ex)
            {
                WriteError(ex.ToString());
            }
        }

        private static async Task ProcessCalendarAsync(IGraphServiceClient graphClient, Calendar calendar)
        {
            WriteInfo();
            WriteInfo($"# {calendar.Name}");

            var request = graphClient.Me.Calendars[calendar.Id].Events.Request()
                .Select("id,subject,start,end,type").Top(100);
            var events = new List<Event>();
            do
            {
                var page = await request.GetAsync();
                events.AddRange(page.CurrentPage);
                Console.Write(events.Count);
                Console.SetCursorPosition(0, Console.CursorTop);
                request = page.NextPageRequest;
            } while (request != null);

            WriteInfo(events.Count + " calendar events.");

            var groups = new Dictionary<GroupByFields, IList<Event>>();
            var count = 0;
            foreach (var @event in events)
            {
                Console.Write($"Checking {++count}...");
                Console.SetCursorPosition(0, Console.CursorTop);

                var groupKey = new GroupByFields(@event.Subject, @event.Start.DateTime, @event.End.DateTime);
                if (!groups.ContainsKey(groupKey))
                {
                    var group = new List<Event> { @event };
                    if (!_options.CheckRecurrance || @event.Type != EventType.SeriesMaster)
                    {
                        var duplicates = events
                            .Where(e => e.Id != @event.Id && new GroupByFields(e.Subject, e.Start.DateTime, e.End.DateTime) == groupKey)
                            .ToList();
                        group.AddRange(duplicates);
                    }
                    else
                    {
                        var otherEventsWithSameSubject = events
                            .Where(e => e.Id != @event.Id && e.Subject == @event.Subject)
                            .ToList();
                        if (otherEventsWithSameSubject.Count > 0)
                        {
                            var instances = await GetInstancesAsync(graphClient, calendar.Id, @event.Id);
                            foreach (var instance in instances)
                            {
                                var duplicates = otherEventsWithSameSubject
                                    .Where(e => e.Start.DateTime == instance.Start.DateTime && e.End.DateTime == instance.End.DateTime)
                                    .ToList();
                                group.AddRange(duplicates);
                            }
                        }
                    }
                    groups.Add(groupKey, group);
                }
            }

            WriteInfo(groups.Count + " groups.");
            var duplicateGroups = groups.Where(g => g.Value.Count() > 1).ToList();
            WriteInfo("Groups with duplicate subject, start, end: " + duplicateGroups.Count);
            foreach (var duplicateGroup in duplicateGroups)
                await ProcessDuplicatesAsync(graphClient, calendar, duplicateGroup);
        }

        private static readonly Dictionary<string, IList<Event>> _instances = [];

        private static async Task<IList<Event>> GetInstancesAsync(IGraphServiceClient graphClient, string calendarId, string eventId)
        {
            try
            {
                if (_instances.ContainsKey(eventId))
                    return _instances[eventId];
                var request = graphClient.Me.Events[eventId].Instances.Request().Select("id,subject,start,end").Top(20);
                var now = DateTimeOffset.Now;
                var endDate = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, now.Offset).AddYears(1);
                var startDate = endDate.AddYears(-5);
                request.QueryOptions.Add(new QueryOption("startDateTime", WebUtility.UrlEncode(startDate.ToString("O"))));
                request.QueryOptions.Add(new QueryOption("endDateTime", WebUtility.UrlEncode(endDate.ToString("O"))));
                var response = await request.GetAsync();
                _instances.Add(eventId, response.CurrentPage);
                return response;
            }
            catch (Exception ex)
            {
                WriteError(ex.ToString());
                return [];
            }
        }

        private static async Task ProcessDuplicatesAsync(IGraphServiceClient graphClient, Calendar calendar, KeyValuePair<GroupByFields, IList<Event>> duplicateGroup)
        {
            WriteInfo($"- {duplicateGroup.Key} ({duplicateGroup.Value.Count()} items)");

            // Check if more than one event have the same ID.
            var idGroups = duplicateGroup.Value.GroupBy(e => e.Id).ToList();
            if (idGroups.Any(g => g.Count() > 1))
            {
                WriteInfo("  The impossible seems to have happened: Multiple events have the same id. Here they are:");
                foreach (var idGroup in idGroups.Where(g => g.Count() > 1))
                {
                    WriteInfo($"  - {idGroup.Count()} events with ID {idGroup.Key}");
                }
            }

            // Double-check for existance so that we don't use "phantom" events.
            var events = await GetNonPhantomsAsync(graphClient, calendar, idGroups.Select(g => g.Key).ToList());
            // Events are sorted with the one to keep first.
            WriteInfo($"  Number of unique \"non-phantom\" IDs: {events.Count}");

            if (events.Count > 1 && _options.Fix)
            {
                if (calendar.CanEdit.HasValue && calendar.CanEdit.Value)
                    await RemoveDuplicatesAsync(graphClient, calendar, events);
                else
                    WriteInfo("  Calendar is not editable so we can't fix.");
            }
        }

        private static async Task<List<Event>> GetNonPhantomsAsync(IGraphServiceClient graphClient, Calendar calendar, IList<string> ids)
        {
            var events = new List<Event>();
            var count = 0;
            foreach (var id in ids)
            {
                Console.Write($"  Checking {++count} of {ids.Count}...");
                Console.SetCursorPosition(0, Console.CursorTop);
                try
                {
                    events.Add(await graphClient.Me.Calendars[calendar.Id].Events[id].Request().GetAsync());
                }
                catch (ServiceException ex)
                {
                    if (ex.Message.Contains("ErrorItemNotFound"))
                        WriteInfo($"  \"Phantom\" event detected. ID: {id}");
                    else
                        throw;
                }
            }

            var orderedEvents = _options.KeepLongestBody
                ? events.OrderByDescending(e => e.Body.Content.Length).ThenByDescending(e => e.LastModifiedDateTime)
                : events.OrderByDescending(e => e.LastModifiedDateTime);
            return [.. orderedEvents];
        }

        private static async Task RemoveDuplicatesAsync(IGraphServiceClient graphClient, Calendar calendar, IList<Event> events)
        {
            var eventToKeep = events.First();
            var eventsToDelete = events.Skip(1).ToList();
            var deleted = 0;
            foreach (var @event in eventsToDelete)
            {
                try
                {
                    await graphClient.Me.Calendars[calendar.Id].Events[@event.Id].Request().DeleteAsync();
                    deleted++;
                    Console.Write($"  {deleted} of {eventsToDelete.Count} deleted.");
                    Console.SetCursorPosition(0, Console.CursorTop);
                }
                catch (ServiceException ex)
                {
                    WriteError(ex.Message.Trim());
                    WriteInfo("Here is a web link: " + @event.WebLink);
                    // If the calendar is read-only, bail out.
                    if (ex.Message.Contains("Read-only calendars can't be modified"))
                        throw;
                }
            }
            WriteInfo($"  {deleted} of {eventsToDelete.Count} deleted.");
        }

        private static void WriteInfo(string message)
        {
            Console.Out.WriteLine(message);
            if (!string.IsNullOrEmpty(_options.Report))
                System.IO.File.AppendAllText(_options.Report, message + Environment.NewLine);
        }

        private static void WriteInfo()
        {
            WriteInfo(string.Empty);
        }

        private static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
            Console.ResetColor();
            if (!string.IsNullOrEmpty(_options.Report))
                System.IO.File.AppendAllText(_options.Report, message + Environment.NewLine);
        }

        private record GroupByFields(string Subject, string Start, string End)
        {
            public override string ToString()
            {
                return $"{Subject} ({Start} - {End})";
            }
        }
    }
}