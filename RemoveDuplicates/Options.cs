using CommandLine;

namespace RemoveDuplicates
{
    internal class Options
    {
        [Option(shortName: 'f', longName: "fix", Default = false, HelpText = "Fix by deleting duplicates")]
        public bool Fix { get; set; }

        [Option(shortName: 'r', longName: "report", HelpText = "Write report to this file name")]
        public string Report { get; set; }

        [Option(shortName: 'c', longName: "calendar", HelpText = "Process only this calendar, e.g. \"Family calendar\".")]
        public string Calendar { get; set; }
    }
}
