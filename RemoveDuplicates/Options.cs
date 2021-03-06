﻿using CommandLine;

namespace RemoveDuplicates
{
    internal class Options
    {
        [Option(shortName: 'f', longName: "fix", Default = false, HelpText = "Fix by deleting duplicates.")]
        public bool Fix { get; set; }

        [Option(shortName: 'r', longName: "report", HelpText = "Write report to a given file, e.g. \"Report.md\".")]
        public string Report { get; set; }

        [Option(shortName: 'c', longName: "calendar", HelpText = "Process only a named calendar, e.g. \"Family calendar\". Otherwise all calendars are processed.")]
        public string Calendar { get; set; }
    }
}
