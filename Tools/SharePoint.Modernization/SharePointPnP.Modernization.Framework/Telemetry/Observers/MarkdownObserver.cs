﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SharePointPnP.Modernization.Framework.Telemetry.Observers
{
    /// <summary>
    /// Markdown observer intended for end-user output
    /// </summary>
    public class MarkdownObserver : ILogObserver
    {

        // Cache the logs between calls
        private static readonly Lazy<List<Tuple<LogLevel,LogEntry>>> _lazyLogInstance = new Lazy<List<Tuple<LogLevel, LogEntry>>>(() => new List<Tuple<LogLevel, LogEntry>>());
        protected bool _includeDebugEntries;
        protected DateTime _reportDate;
        protected string _reportFileName = "";
        protected string _reportFolder = Environment.CurrentDirectory;
        protected string _pageBeingTransformed;

        #region Construction
        /// <summary>
        /// Constructor for specifying to include debug entries
        /// </summary>
        /// <param name="fileName">Name used to construct the log file name</param>
        /// <param name="folder">Folder that will hold the log file</param>
        /// <param name="includeDebugEntries">Include Debug Log Entries</param>
        public MarkdownObserver(string fileName = "", string folder = "", bool includeDebugEntries = false)
        {
            _includeDebugEntries = includeDebugEntries;
            _reportDate = DateTime.Now;

            if (!string.IsNullOrEmpty(folder))
            {
                _reportFolder = folder;
            }

            // Drop possible file extension as we want to ensure we have a .md extension
            _reportFileName = System.IO.Path.GetFileNameWithoutExtension(fileName);

#if DEBUG && MEASURE && MEASURE
           _includeDebugEntries = true; //Override for debugging locally
#endif
        }
        #endregion

        #region Markdown Tokens
        private const string Heading1 = "#";
        private const string Heading2 = "##";
        private const string Heading3 = "###";
        private const string Heading4 = "####";
        private const string Heading5 = "#####";
        private const string Heading6 = "######";
        private const string UnorderedListItem = "-";
        private const string Italic = "_";
        private const string Bold = "**";
        private const string BlockQuotes = "> ";
        private const string TableHeaderColumn = "-------------";
        private const string TableColumnSeperator = " | ";
        private const string Link = "[{0}]({1})";
        #endregion

        /// <summary>
        /// Get the single List<LogEntry> instance, singleton pattern
        /// </summary>
        public static List<Tuple<LogLevel, LogEntry>> Logs
        {
            get
            {
                return _lazyLogInstance.Value;
            }
        }

        /// <summary>
        /// Debug level of data not recorded unless in debug mode
        /// </summary>
        /// <param name="entry"></param>
        public void Debug(LogEntry entry)
        {
            if (_includeDebugEntries)
            {
                entry.PageName = this._pageBeingTransformed;
                Logs.Add(new Tuple<LogLevel, LogEntry>(LogLevel.Debug, entry));
            }
        }

        /// <summary>
        /// Errors 
        /// </summary>
        /// <param name="entry"></param>
        public void Error(LogEntry entry)
        {
            entry.PageName = this._pageBeingTransformed;
            Logs.Add(new Tuple<LogLevel, LogEntry>(LogLevel.Error, entry));
        }

        /// <summary>
        /// Reporting operations throughout the transform process
        /// </summary>
        /// <param name="entry"></param>
        public void Info(LogEntry entry)
        {
            entry.PageName = this._pageBeingTransformed;
            Logs.Add(new Tuple<LogLevel, LogEntry>(LogLevel.Information, entry));
        }

        /// <summary>
        /// Report on any warnings generated by the reporting tool
        /// </summary>
        /// <param name="entry"></param>
        public void Warning(LogEntry entry)
        {
            entry.PageName = this._pageBeingTransformed;
            Logs.Add(new Tuple<LogLevel, LogEntry>(LogLevel.Warning, entry));
        }

        /// <summary>
        /// Sets the name of the page that's being transformed
        /// </summary>
        /// <param name="pageName">Name of the page</param>
        public void SetPage(string pageName)
        {
            this._pageBeingTransformed = pageName;
        }

        /// <summary>
        /// Generates a markdown based report based on the logs
        /// </summary>
        /// <returns></returns>
        protected virtual string GenerateReport(bool includeHeading = true)
        {
            StringBuilder report = new StringBuilder();

            // Get one log entry per page...assumes that this log entry is included by each transformator
            var distinctLogs = Logs.Where(p => p.Item2.Heading == LogStrings.Heading_Summary && p.Item2.Message.StartsWith(LogStrings.TransformingSite));

            bool first = true;
            foreach(var distinctLogEntry in distinctLogs)
            {
                var logEntriesToProcess = Logs.Where(p => p.Item2.PageName == distinctLogEntry.Item2.PageName);
                GenerateReportForPage(report, logEntriesToProcess, first);
                first = false;
            }

            return report.ToString();
        }

        /// <summary>
        /// Generates a markdown based report based on the logs
        /// </summary>
        /// <returns></returns>
        private string GenerateReportForPage(StringBuilder report, IEnumerable<Tuple<LogLevel, LogEntry>> logEntriesToProcess, bool includeHeading = true)
        {
            if (includeHeading)
            {
                report.AppendLine($"{Heading1} Modernisation Report");
                report.AppendLine();
            }

            // This could display something cool here e.g. Time taken to transform and transformation options e.g. PageTransformationInformation details
            var reportDate = _reportDate;
            var allLogs = logEntriesToProcess.OrderBy(l => l.Item2.EntryTime);

            report.AppendLine($"{Heading2} Transformation Details");
            report.AppendLine();
            report.AppendLine($"{UnorderedListItem} Report date: {reportDate}");
            var logStart = allLogs.FirstOrDefault();
            var logEnd = allLogs.LastOrDefault();

            if (logStart != default(Tuple<LogLevel,LogEntry>) && logEnd != default(Tuple<LogLevel, LogEntry>))
            {
                TimeSpan span = logEnd.Item2.EntryTime.Subtract(logStart.Item2.EntryTime);
                report.AppendLine($"{UnorderedListItem} Transform duration: {string.Format("{0:D2}:{1:D2}:{2:D2}", span.Hours, span.Minutes, span.Seconds)}");
            }

            var transformationSummary = allLogs.Where(l => l.Item2.Heading == LogStrings.Heading_Summary);

            foreach (var log in transformationSummary)
            {
                report.AppendLine($"{UnorderedListItem} {log.Item2.Message}");
            }

            #region Summary Page Transformation Information Settings

            report.AppendLine();
            report.AppendLine($"{Heading3} Page Transformation Settings");
            report.AppendLine();
            report.AppendLine($"Property {TableColumnSeperator} Setting");
            report.AppendLine($"{TableHeaderColumn} {TableColumnSeperator} {TableHeaderColumn}");

            var transformationSettings = allLogs.Where(l => l.Item2.Heading == LogStrings.Heading_PageTransformationInfomation);
            foreach (var log in transformationSettings)
            {
                var keyValue = log.Item2.Message.Split(new string[] { LogStrings.KeyValueSeperatorToken }, StringSplitOptions.None);
                if (keyValue.Length == 2) //Protect output
                {
                    report.AppendLine($"{keyValue[0] ?? ""} {TableColumnSeperator} {keyValue[1] ?? "<Not Set>"}");
                }
            }

            #endregion

            report.AppendLine($"{Heading2} Transformation Operation Summary");
            report.AppendLine();

            #region Transformation Summary

            report.AppendLine($"Date {TableColumnSeperator} Operation {TableColumnSeperator} Actions Performed");
            report.AppendLine($"{TableHeaderColumn} {TableColumnSeperator} {TableHeaderColumn} {TableColumnSeperator} {TableHeaderColumn} ");

            var logDetails = allLogs.Where(l => l.Item2.Heading != LogStrings.Heading_PageTransformationInfomation &&
                                                l.Item2.Heading != LogStrings.Heading_Summary);

            IEnumerable<Tuple<LogLevel, LogEntry>> filteredLogDetails = null;
            if (_includeDebugEntries)
            {
                filteredLogDetails = logDetails.Where(l => l.Item1 == LogLevel.Debug ||
                                                           l.Item1 == LogLevel.Information ||
                                                           l.Item1 == LogLevel.Warning);
            }
            else
            {
                filteredLogDetails = logDetails.Where(l => l.Item1 == LogLevel.Information ||
                                                           l.Item1 == LogLevel.Warning);
            }

            foreach (var log in filteredLogDetails)
            {
                switch(log.Item1)
                {
                    case LogLevel.Information:
                        report.AppendLine($"{log.Item2.EntryTime} {TableColumnSeperator} {log.Item2.Heading} {TableColumnSeperator} {log.Item2.Message}");
                        break;
                    case LogLevel.Warning:
                        report.AppendLine($"{log.Item2.EntryTime} {TableColumnSeperator} {Bold}{log.Item2.Heading}{Bold} {TableColumnSeperator} {Bold}{log.Item2.Message}{Bold}");
                        break;
                    case LogLevel.Debug:
                        report.AppendLine($"{log.Item2.EntryTime} {TableColumnSeperator} {Italic}{log.Item2.Heading}{Italic} {TableColumnSeperator} {Italic}{log.Item2.Message}{Italic}");
                        break;
                }
            }

            #endregion

            if (logDetails.Any(l => l.Item1 == LogLevel.Error))
            {
                #region Report on Errors

                report.AppendLine($"{Heading3} Errors occurred during transformation");
                report.AppendLine();

                report.AppendLine($"Date {TableColumnSeperator} Operation {TableColumnSeperator} Error Message");
                report.AppendLine($"{TableHeaderColumn} {TableColumnSeperator} {TableHeaderColumn} {TableColumnSeperator} {TableHeaderColumn}");

                foreach (var log in logDetails.Where(l => l.Item1 == LogLevel.Error))
                {
                    report.AppendLine($"{log.Item2.EntryTime} {TableColumnSeperator} {log.Item2.Heading} {TableColumnSeperator} {log.Item2.Message}");
                }

                #endregion

            }

            return report.ToString();
        }

        /// <summary>
        /// Output the report when flush is called
        /// </summary>
        public virtual void Flush()
        {
            try
            {
                var report = GenerateReport();

                // Dont want to assume locality here
                string logRunTime = _reportDate.ToString().Replace('/', '-').Replace(":", "-").Replace(" ", "-");
                string logFileName = $"Page-Transformation-Report-{logRunTime}{_reportFileName}";

                logFileName = $"{_reportFolder}\\{logFileName}.md";

                using (StreamWriter sw = new StreamWriter(logFileName, true))
                {
                    sw.WriteLine(report);
                }

                // Cleardown all logs
                var logs = _lazyLogInstance.Value;
                logs.RemoveRange(0, logs.Count);

                Console.WriteLine($"Report saved as: {logFileName}");

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing to log file: {0} {1}", ex.Message, ex.StackTrace);
            }

        }

    }
}
