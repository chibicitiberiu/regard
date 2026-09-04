using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Backend.Tests.Utils
{
    /// <summary>
    /// The parser carries all the risk in the log viewer, and every case here is one that actually
    /// occurs in the ~80,000 lines on the dev install rather than one imagined for coverage. Getting any
    /// of them wrong is silent: the page still renders, it just shows something that isn't what was
    /// logged.
    /// </summary>
    [TestClass]
    public class LogLineParserTests
    {
        // Real shapes, trimmed. Note the trailing space before the (absent) exception -- NLog always
        // writes one.
        private const string NewLine =
            "2026-09-04 02:15:20.1234|0|INFO|Regard.Backend.Jobs.SynchronizeJob|Regard.Backend.Jobs.SynchronizeJob.ExecuteJob|||Synchronization started. ";

        private const string OldLine =
            "2026-08-29 00:00:00.0051|0|INFO|Regard.Backend.Jobs.SynchronizeJob|Synchronization started. ";

        private static LogEntry ParseOne(string line)
        {
            Assert.IsTrue(LogLineParser.TryParseHeader(line, out var entry), $"failed to parse: {line}");
            return entry;
        }

        // ---------------------------------------------------------------- the two layouts

        [TestMethod]
        public void The_eight_field_layout_is_read_in_full()
        {
            var e = ParseOne(NewLine);

            Assert.AreEqual(new DateTime(2026, 9, 4, 2, 15, 20, 123).AddTicks(4000), e.Timestamp);
            Assert.AreEqual(LogSeverity.Info, e.Severity);
            Assert.AreEqual("INFO", e.Level);
            Assert.AreEqual("Regard.Backend.Jobs.SynchronizeJob", e.Logger);
            Assert.AreEqual("Regard.Backend.Jobs.SynchronizeJob.ExecuteJob", e.Callsite);
            Assert.IsNull(e.RequestUrl);
            Assert.IsNull(e.MvcAction);
            Assert.AreEqual("Synchronization started.", e.Message);
        }

        [TestMethod]
        public void The_five_field_layout_still_reads_and_simply_has_no_callsite()
        {
            var e = ParseOne(OldLine);

            Assert.AreEqual(LogSeverity.Info, e.Severity);
            Assert.AreEqual("Regard.Backend.Jobs.SynchronizeJob", e.Logger);
            Assert.AreEqual("Synchronization started.", e.Message);
            Assert.IsNull(e.Callsite, "there was no callsite field before Batch 6a");
        }

        [TestMethod]
        public void Both_layouts_can_appear_in_the_same_stream()
        {
            // Exactly what regard-2026-09-03.log looks like across the restart that changed the config.
            var entries = LogLineParser.Parse(new[] { OldLine, NewLine }).ToList();

            Assert.AreEqual(2, entries.Count);
            Assert.IsNull(entries[0].Callsite);
            Assert.IsNotNull(entries[1].Callsite);
            Assert.AreEqual("Synchronization started.", entries[0].Message);
            Assert.AreEqual("Synchronization started.", entries[1].Message);
        }

        [TestMethod]
        public void A_request_scoped_entry_keeps_its_url_and_action()
        {
            var e = ParseOne("2026-09-04 02:15:20.1234|0|WARN|Some.Logger|Some.Logger.Method|"
                           + "http://localhost/api/auth/login|AuthController.Login|Failed to determine the https port. ");

            Assert.AreEqual("http://localhost/api/auth/login", e.RequestUrl);
            Assert.AreEqual("AuthController.Login", e.MvcAction);
            Assert.AreEqual("Failed to determine the https port.", e.Message);
        }

        // ---------------------------------------------------------------- pipes inside messages

        [TestMethod]
        public void A_message_containing_a_pipe_survives_in_the_new_layout()
        {
            var e = ParseOne("2026-09-04 02:15:20.1234|0|INFO|L.G|L.G.M|||New video (15:0:Swimming | Episode 2) ");
            Assert.AreEqual("New video (15:0:Swimming | Episode 2)", e.Message);
        }

        [TestMethod]
        public void A_message_containing_a_pipe_survives_in_the_old_layout()
        {
            // 681 lines in one real file look like this.
            var e = ParseOne("2026-08-30 22:16:32.2859|0|INFO|Regard.Backend.Jobs.SynchronizeJob|"
                           + "New video (15:0:LMG Learns Life Skills - Swimming | Episode 2) ");

            Assert.IsNull(e.Callsite);
            Assert.AreEqual("New video (15:0:LMG Learns Life Skills - Swimming | Episode 2)", e.Message);
        }

        /// <summary>
        /// The assertion that protects 69,815 real lines. An old-format message with enough pipes fills
        /// all eight slots of the bounded split, so field count alone cannot tell the layouts apart —
        /// it is the shape of field 5 that does. Get this wrong and old entries silently lose the front
        /// of their message to a "callsite" column.
        /// </summary>
        [TestMethod]
        public void An_old_message_with_several_pipes_is_not_mistaken_for_the_new_layout()
        {
            var e = ParseOne("2026-08-30 22:16:32.2859|0|INFO|Regard.Backend.Jobs.SynchronizeJob|"
                           + "New video (a|b|c|d) ");

            Assert.IsNull(e.Callsite, "field 5 is prose, so this is the old layout");
            Assert.AreEqual("New video (a|b|c|d)", e.Message);
        }

        [TestMethod]
        public void A_pipe_heavy_message_with_no_spaces_still_needs_a_dotted_callsite_to_look_new()
        {
            // Worst case for the heuristic: no whitespace anywhere in field 5. The dot requirement is
            // what saves it, because a bare token is not a Namespace.Type.Method.
            var e = ParseOne("2026-08-30 22:16:32.2859|0|INFO|Some.Logger|abc|def|ghi|jkl ");

            Assert.IsNull(e.Callsite);
            Assert.AreEqual("abc|def|ghi|jkl", e.Message);
        }

        // ---------------------------------------------------------------- continuation lines

        [TestMethod]
        public void Continuation_lines_are_folded_into_the_entry_above()
        {
            var lines = new[]
            {
                "2026-08-29 12:00:00.0000|0|ERROR|Some.Logger|Backup failed. System.Exception: boom",
                "   at Regard.Backend.Services.DatabaseBackupService.CreateBackupAsync()",
                "   at Regard.Backend.Jobs.MaintenanceJob.RunBackup()",
                NewLine,
            };

            var entries = LogLineParser.Parse(lines).ToList();

            Assert.AreEqual(2, entries.Count, "three lines belong to one entry");
            StringAssert.Contains(entries[0].Detail, "DatabaseBackupService.CreateBackupAsync");
            StringAssert.Contains(entries[0].Detail, "MaintenanceJob.RunBackup");
            Assert.IsTrue(entries[0].HasDetail);
            Assert.IsFalse(entries[1].HasDetail);
        }

        [TestMethod]
        public void A_continuation_before_any_header_is_discarded_rather_than_throwing()
        {
            // Happens when a file rolls in the middle of a stack trace.
            var entries = LogLineParser.Parse(new[] { "   at Something.Orphaned()", OldLine }).ToList();

            Assert.AreEqual(1, entries.Count);
            Assert.IsFalse(entries[0].HasDetail);
        }

        [TestMethod]
        public void Blank_lines_are_ignored_and_do_not_become_detail()
        {
            var entries = LogLineParser.Parse(new[] { OldLine, "", "   ", NewLine }).ToList();

            Assert.AreEqual(2, entries.Count);
            Assert.IsFalse(entries[0].HasDetail, "a blank line is not part of the exception");
        }

        // ---------------------------------------------------------------- malformed input

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("no pipes here at all")]
        [DataRow("|leading pipe")]
        [DataRow("not-a-timestamp|0|INFO|Logger|message")]
        [DataRow("2026-13-45 99:99:99.0000|0|INFO|Logger|message")]
        [DataRow("2026-09-04 02:15:20.1234")]
        public void Lines_that_are_not_headers_are_rejected_cleanly(string line)
        {
            Assert.IsFalse(LogLineParser.TryParseHeader(line, out _));
        }

        [TestMethod]
        public void A_truncated_final_line_does_not_throw()
        {
            // The file is being appended to while we read it, so the last line can be half-written.
            var entries = LogLineParser.Parse(new[] { OldLine, "2026-09-04 02:15:20.1234|0|IN" }).ToList();

            Assert.AreEqual(1, entries.Count, "the half-line is not a valid header, so it lands as detail");
            StringAssert.Contains(entries[0].Detail, "2026-09-04");
        }

        [TestMethod]
        public void Carriage_returns_are_trimmed()
        {
            var e = ParseOne(OldLine.TrimEnd() + "\r");
            Assert.IsFalse(e.Message.EndsWith("\r"), "a CRLF file must not leave \\r in the message");
        }

        [TestMethod]
        public void Null_input_yields_nothing()
        {
            Assert.AreEqual(0, LogLineParser.Parse(null).Count());
            Assert.IsFalse(LogLineParser.TryParseHeader(null, out _));
        }

        // ---------------------------------------------------------------- severity

        [TestMethod]
        [DataRow("TRACE", LogSeverity.Trace)]
        [DataRow("DEBUG", LogSeverity.Debug)]
        [DataRow("INFO", LogSeverity.Info)]
        [DataRow("WARN", LogSeverity.Warn)]
        [DataRow("ERROR", LogSeverity.Error)]
        [DataRow("FATAL", LogSeverity.Fatal)]
        [DataRow("SOMETHING", LogSeverity.Unknown)]
        [DataRow("", LogSeverity.Unknown)]
        public void Severity_is_read_from_the_level_field(string level, LogSeverity expected)
        {
            Assert.AreEqual(expected, LogLineParser.ParseSeverity(level));
        }

        [TestMethod]
        public void An_unrecognised_level_keeps_its_original_text()
        {
            var e = ParseOne("2026-09-04 02:15:20.1234|0|WEIRD|Some.Logger|Some.Logger.M|||message ");

            Assert.AreEqual(LogSeverity.Unknown, e.Severity);
            Assert.AreEqual("WEIRD", e.Level);
        }
    }

    [TestClass]
    public class LogQueryRunnerTests
    {
        private static readonly DateTime Base = new DateTime(2026, 9, 4, 12, 0, 0);

        // AddMinutes rather than a minute component: the sequences below go past 59.
        private static LogEntry E(int minute, LogSeverity severity, string message, string logger = "L.G",
                                 string detail = null)
            => new LogEntry
            {
                Timestamp = Base.AddMinutes(minute),
                Severity = severity,
                Level = severity.ToString().ToUpperInvariant(),
                Logger = logger,
                Message = message,
                Detail = detail,
            };

        private static List<LogEntry> Sequence(int count)
            => Enumerable.Range(0, count)
                .Select(i => E(i, LogSeverity.Info, $"message {i}"))
                .ToList();

        [TestMethod]
        public void Entries_come_back_newest_first()
        {
            var page = LogQueryRunner.Run(Sequence(5), new LogQuery { Take = 5 });

            CollectionAssert.AreEqual(
                new[] { "message 4", "message 3", "message 2", "message 1", "message 0" },
                page.Entries.Select(e => e.Message).ToArray());
        }

        [TestMethod]
        public void Paging_walks_backwards_through_the_file()
        {
            var all = Sequence(10);

            var first = LogQueryRunner.Run(all, new LogQuery { Skip = 0, Take = 3 });
            var second = LogQueryRunner.Run(all, new LogQuery { Skip = 3, Take = 3 });

            CollectionAssert.AreEqual(new[] { "message 9", "message 8", "message 7" },
                                      first.Entries.Select(e => e.Message).ToArray());
            CollectionAssert.AreEqual(new[] { "message 6", "message 5", "message 4" },
                                      second.Entries.Select(e => e.Message).ToArray());
        }

        [TestMethod]
        public void The_total_counts_matches_not_the_page()
        {
            var page = LogQueryRunner.Run(Sequence(100), new LogQuery { Skip = 0, Take = 10 });

            Assert.AreEqual(10, page.Entries.Count);
            Assert.AreEqual(100, page.TotalMatched, "the pager needs the full count, not the page size");
        }

        [TestMethod]
        public void Paging_past_the_end_returns_nothing_rather_than_failing()
        {
            var page = LogQueryRunner.Run(Sequence(5), new LogQuery { Skip = 500, Take = 10 });

            Assert.AreEqual(0, page.Entries.Count);
            Assert.AreEqual(5, page.TotalMatched);
        }

        [TestMethod]
        public void A_min_severity_drops_everything_below_it()
        {
            var entries = new[]
            {
                E(0, LogSeverity.Debug, "debug"),
                E(1, LogSeverity.Info, "info"),
                E(2, LogSeverity.Warn, "warn"),
                E(3, LogSeverity.Error, "error"),
            };

            var page = LogQueryRunner.Run(entries, new LogQuery { MinSeverity = LogSeverity.Warn, Take = 10 });

            CollectionAssert.AreEqual(new[] { "error", "warn" }, page.Entries.Select(e => e.Message).ToArray());
            Assert.AreEqual(2, page.TotalMatched);
        }

        /// <summary>
        /// Unknown sorts above Fatal on purpose: a level we cannot classify should not vanish behind a
        /// filter, because it is more likely to be a problem than routine chatter.
        /// </summary>
        [TestMethod]
        public void An_unknown_level_is_never_hidden_by_a_min_severity_filter()
        {
            var entries = new[] { E(0, LogSeverity.Unknown, "strange") };

            var page = LogQueryRunner.Run(entries, new LogQuery { MinSeverity = LogSeverity.Error, Take = 10 });

            Assert.AreEqual(1, page.Entries.Count);
        }

        [TestMethod]
        public void Search_matches_message_logger_and_exception_detail()
        {
            var entries = new[]
            {
                E(0, LogSeverity.Info, "nothing interesting"),
                E(1, LogSeverity.Info, "found in message"),
                E(2, LogSeverity.Info, "plain", logger: "Regard.Needle.Service"),
                E(3, LogSeverity.Error, "boom", detail: "   at Something.Needle()"),
            };

            var page = LogQueryRunner.Run(entries, new LogQuery { Search = "needle", Take = 10 });

            Assert.AreEqual(2, page.TotalMatched, "logger and stack trace both count as matches");
        }

        [TestMethod]
        public void Search_is_case_insensitive()
        {
            var page = LogQueryRunner.Run(new[] { E(0, LogSeverity.Info, "Synchronization STARTED") },
                                          new LogQuery { Search = "started", Take = 10 });
            Assert.AreEqual(1, page.TotalMatched);
        }

        [TestMethod]
        public void An_empty_search_matches_everything()
        {
            foreach (var search in new[] { null, "", "   " })
                Assert.AreEqual(5, LogQueryRunner.Run(Sequence(5),
                    new LogQuery { Search = search, Take = 10 }).TotalMatched, $"search={search ?? "null"}");
        }

        [TestMethod]
        public void A_take_of_zero_still_reports_the_total()
        {
            // The UI asks for this when it only wants the count for the pager.
            var page = LogQueryRunner.Run(Sequence(50), new LogQuery { Take = 0 });

            Assert.AreEqual(0, page.Entries.Count);
            Assert.AreEqual(50, page.TotalMatched);
        }

        [TestMethod]
        public void The_window_is_capped_so_a_deep_page_cannot_buffer_the_whole_file()
        {
            var page = LogQueryRunner.Run(Sequence(200),
                new LogQuery { Skip = LogQueryRunner.MaxWindow + 5000, Take = 10 });

            Assert.AreEqual(0, page.Entries.Count);
            Assert.AreEqual(200, page.TotalMatched, "the count is still honest even past the cap");
        }

        [TestMethod]
        public void Null_and_empty_input_are_tolerated()
        {
            Assert.AreEqual(0, LogQueryRunner.Run(null, new LogQuery()).TotalMatched);
            Assert.AreEqual(0, LogQueryRunner.Run(Array.Empty<LogEntry>(), null).TotalMatched);
        }
    }
}
