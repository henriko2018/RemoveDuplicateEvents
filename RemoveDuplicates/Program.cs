using CommandLine;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using RemoveDuplicates;


Options _options = new();
string[] _selectFields = ["id", "subject", "start", "end"];

await Parser.Default.ParseArguments<Options>(args)
    .WithParsedAsync(opts =>
    {
        _options = opts;
        return RunOptionsAndReturnExitCodeAsync();
    });


async Task RunOptionsAndReturnExitCodeAsync()
{
    try
    {
        var graphClient = await AuthenticationHelper.GetAuthenticatedClientAsync();
        if (graphClient != null)
        {
            WriteInfo("# Calendars:");
            var calendars = (await graphClient.Me.Calendars.GetAsync())?.Value
                ?? throw new ApplicationException("No calendars response.");
            foreach (var calendar in calendars)
            {
                WriteInfo($"- {calendar.Name}");
            }

            if (string.IsNullOrEmpty(_options?.Calendar))
            {
                WriteInfo("Use -h or --help to view options.");
            }
            else
            {
                var calendar = calendars.SingleOrDefault(c => _options.Calendar.Equals(c.Name, StringComparison.CurrentCultureIgnoreCase));
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

async Task ProcessCalendarAsync(GraphServiceClient graphClient, Calendar calendar)
{
    WriteInfo(string.Empty);
    WriteInfo($"# {calendar.Name}");

    List<Event> events;
    if (_options.UseCalendarView)
        events = await GetCalendarViewAsync(graphClient, calendar);
    else
        events = await GetEventsAsync(graphClient, calendar);
    WriteInfo(events.Count + " calendar events.");

    var groups = events.GroupBy(e => new GroupByFields(e.Subject, e.Start?.DateTime, e.End?.DateTime))
        .OrderBy(g => g.Key.Start)
        .ToList();
    WriteInfo(groups.Count + " groups.");
    var duplicateGroups = groups.Where(g => g.Count() > 1).ToList();
    WriteInfo("Groups with duplicate subject, start, end: " + duplicateGroups.Count);
    foreach (var duplicateGroup in duplicateGroups)
    {
        await ProcessDuplicatesAsync(graphClient, calendar, duplicateGroup);
    }
}

async Task<List<Event>> GetEventsAsync(GraphServiceClient graphClient, Calendar calendar)
{
    var events = new List<Event>();
    var response = await graphClient.Me.Calendars[calendar.Id].Events
        .GetAsync(requestConfig =>
        {
            requestConfig.QueryParameters.Select = _selectFields;
            requestConfig.QueryParameters.Top = 100;
        });

    while (true)
    {
        if (response?.Value is not null)
        {
            events.AddRange(response.Value);
            Console.Write(events.Count);
            Console.SetCursorPosition(0, Console.CursorTop);
            if (response.OdataNextLink is null)
                break;
            else
                response = await graphClient.Me.Events.WithUrl(response.OdataNextLink).GetAsync();
        }
        else
        {
            WriteError("No events response.");
            break;
        }
    }

    return events;
}

async Task<List<Event>> GetCalendarViewAsync(GraphServiceClient graphClient, Calendar calendar)
{
    var events = new List<Event>();
    // startDateTime and endDateTime must be specified and the maximum time range is 5 years.
    // Use the current year.
    var now = DateTimeOffset.Now;
    var startDateTime = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, now.Offset);
    var endDateTime = startDateTime.AddYears(1);
    var response = await graphClient.Me.Calendars[calendar.Id].CalendarView
        .GetAsync(requestConfig =>
        {
            requestConfig.QueryParameters.Select = _selectFields;
            requestConfig.QueryParameters.Top = 100;
            requestConfig.QueryParameters.StartDateTime = startDateTime.ToString("o");
            requestConfig.QueryParameters.EndDateTime = endDateTime.ToString("o");
        });

    while (true)
    {
        if (response?.Value is not null)
        {
            events.AddRange(response.Value);
            Console.Write(events.Count);
            Console.SetCursorPosition(0, Console.CursorTop);
            if (response.OdataNextLink is null)
                break;
            else
                response = await graphClient.Me.Calendars[calendar.Id].CalendarView.WithUrl(response.OdataNextLink).GetAsync();
        }
        else
        {
            WriteError("No CalendarView response.");
            break;
        }
    }

    return events;
}

async Task ProcessDuplicatesAsync(GraphServiceClient graphClient, Calendar calendar, IGrouping<GroupByFields, Event> duplicateGroup)
{
    WriteInfo($"- {duplicateGroup.Key} ({duplicateGroup.Count()} items)");

    // Check if more than one event have the same ID.
    var idGroups = duplicateGroup.GroupBy(e => e.Id).ToList();
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

    if (events.Count > 1)
    {
        if (calendar.CanEdit.HasValue && calendar.CanEdit.Value)
            await RemoveDuplicatesAsync(graphClient, calendar, events);
        else
            WriteInfo("  Calendar is not editable so we can't fix.");
    }
}

async Task<List<Event>> GetNonPhantomsAsync(GraphServiceClient graphClient, Calendar calendar, IList<string?> ids)
{
    var events = new List<Event>();
    var count = 0;
    foreach (var id in ids)
    {
        Console.Write($"  Checking {++count} of {ids.Count}...");
        Console.SetCursorPosition(0, Console.CursorTop);
        try
        {
            var @event = await graphClient.Me.Calendars[calendar.Id].Events[id].GetAsync();
            if (@event is not null)
                events.Add(@event);
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
        ? events.OrderByDescending(e => e.Body?.Content?.Length).ThenByDescending(e => e.LastModifiedDateTime)
        : events.OrderByDescending(e => e.LastModifiedDateTime);
    return [.. orderedEvents];
}

async Task RemoveDuplicatesAsync(GraphServiceClient graphClient, Calendar calendar, IList<Event> events)
{
    var eventToKeep = events.First();
    WriteInfo("  Keeping last modified " + eventToKeep.LastModifiedDateTime);
    var eventsToDelete = events.Skip(1).ToList();
    var deleted = 0;
    foreach (var @event in eventsToDelete)
    {
        WriteInfo("  Deleting last modified " + @event.LastModifiedDateTime);
        try
        {
            if (_options.Fix)
            {
                await graphClient.Me.Calendars[calendar.Id].Events[@event.Id].DeleteAsync();
                deleted++;
                //Console.Write($"  {deleted} of {eventsToDelete.Count} deleted.");
                //Console.SetCursorPosition(0, Console.CursorTop);
            }
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

void WriteInfo(string message)
{
    Console.Out.WriteLine(message);
    if (!string.IsNullOrEmpty(_options?.Report))
        System.IO.File.AppendAllText(_options.Report, message + Environment.NewLine);
}

void WriteError(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine(message);
    Console.ResetColor();
    if (!string.IsNullOrEmpty(_options.Report))
        System.IO.File.AppendAllText(_options.Report, message + Environment.NewLine);
}

readonly struct GroupByFields(string? subject, string? start, string? end)
{
    public readonly string? Subject { get; } = subject;
    public readonly string? Start { get; } = start;
    public readonly string? End { get; } = end;

    public override string ToString()
    {
        DateTimeOffset? start = Start is null ? null : DateTimeOffset.Parse(Start);
        DateTimeOffset? end = End is null ? null : DateTime.Parse(End);
        return $"{Subject} ({start:g} - {end:g})";
    }
}