using CommandLine;

namespace RemoveDuplicates
{
    internal class Options
    {
        [Option(shortName: 'f', longName: "fix", Default = false, HelpText = "Fix by deleting duplicates.")]
        public bool Fix { get; set; }

        [Option(shortName: 'r', longName: "report", HelpText = "Write report to a given file, e.g. \"Report.md\".")]
        public string Report { get; set; }

        [Option(shortName: 'c', longName: "calendar", HelpText = "Process a named calendar, e.g. \"Family calendar\".")]
        public string Calendar { get; set; }

        [Option(longName: "keepLongestBody", HelpText = "Keep duplicate with longest body. (Default is keep last modified.)")]
        public bool KeepLongestBody { get; set; }

        [Option(longName: "checkRecurrence", HelpText = "Check for recurring events.")]
        public bool CheckRecurrance { get; set; }

        [Option(longName: "keepRecurring", HelpText = "Keep recurring event. (Default is keep last modified.)")]
        public bool KeepRecurring { get; set; }
    }
}
