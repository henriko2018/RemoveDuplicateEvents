using CommandLine;

namespace RemoveDuplicates
{
    internal class Options
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        [Option(shortName: 'f', longName: "fix", Default = false, HelpText = "Fix by deleting duplicates.")]
        public bool Fix { get; set; }

        [Option(shortName: 'r', longName: "report", HelpText = "Write report to a given file, e.g. \"Report.md\".")]
        public string Report { get; set; }

        [Option(shortName: 'c', longName: "calendar", HelpText = "Process a named calendar, e.g. \"Family calendar\".")]
        public string Calendar { get; set; }

        [Option(shortName: 'b', longName: "keepLongestBody", HelpText = "Keep duplicate with longest body. (Default is keep last modified.)")]
        public bool KeepLongestBody { get; set; }

        [Option(shortName: 'v', longName: "useCalendarView", HelpText = "Use calendar view, which considers recurring events. (Default is use get events.)")]
        public bool UseCalendarView { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    }
}
