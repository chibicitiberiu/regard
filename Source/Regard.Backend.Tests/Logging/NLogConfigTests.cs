using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using NLog.Config;
using NLog.Targets;
using System.IO;
using System.Linq;

namespace Regard.Backend.Tests.Logging
{
    /// <summary>
    /// Loads every shipped nlog.config for real.
    ///
    /// This exists because nothing else does. The Dockerfile only <c>COPY</c>s
    /// Docker/Backend/nlog-Release.config into the image — it never loads it — and CI builds and
    /// pushes that image on every push to master. Combined with <c>throwConfigExceptions="true"</c>
    /// and the fact that Program.cs calls SetupLogger() *outside* its try block, a typo in a config
    /// is an unhandled startup crash with no log output, and it would ship as :latest first.
    ///
    /// The configs also have to stay in step with each other, and one of them (nlog-Debug.config) is
    /// not referenced by anything, so it would otherwise rot unnoticed.
    /// </summary>
    [TestClass]
    public class NLogConfigTests
    {
        private static string ConfigPath(string name)
            => Path.Combine(Path.GetDirectoryName(typeof(NLogConfigTests).Assembly.Location), "NLogConfigs", name);

        private static readonly string[] AllConfigs =
        {
            "nlog.config",           // local dev run
            "nlog-Release.config",   // what the Docker image actually runs
            "nlog-Debug.config",     // unused today, kept as the verbose image variant
        };

        private static LoggingConfiguration Load(string name)
        {
            var path = ConfigPath(name);
            Assert.IsTrue(File.Exists(path), $"{name} was not copied to the test output ({path})");

            // optional:false so a missing file fails loudly rather than yielding an empty config that
            // would pass every assertion below by accident.
            return new LogFactory().Setup()
                .LoadConfigurationFromFile(path, optional: false)
                .LogFactory.Configuration;
        }

        [TestMethod]
        [DataRow("nlog.config")]
        [DataRow("nlog-Release.config")]
        [DataRow("nlog-Debug.config")]
        public void Config_loads_without_throwing(string name)
        {
            var config = Load(name);
            Assert.IsNotNull(config, $"{name} produced no configuration");
        }

        [TestMethod]
        [DataRow("nlog.config")]
        [DataRow("nlog-Release.config")]
        [DataRow("nlog-Debug.config")]
        public void Config_has_the_file_target_and_a_console(string name)
        {
            var config = Load(name);

            var file = config.FindTargetByName<FileTarget>("txtFile");
            Assert.IsNotNull(file, $"{name} has no txtFile target");
            Assert.IsNotNull(config.FindTargetByName("console"), $"{name} has no console target");
        }

        /// <summary>
        /// The csvFile target duplicated every event into a second file that nothing read. Removing it
        /// means removing the target AND both rules that named it — miss either and NLog throws
        /// "Target 'csvFile' not found for logging rule" at load. That failure mode is the reason this
        /// test exists, so assert it from both directions.
        /// </summary>
        [TestMethod]
        [DataRow("nlog.config")]
        [DataRow("nlog-Release.config")]
        [DataRow("nlog-Debug.config")]
        public void Config_no_longer_has_the_duplicate_csv_target_or_any_rule_naming_it(string name)
        {
            var config = Load(name);

            Assert.IsNull(config.FindTargetByName("csvFile"), $"{name} still declares the csvFile target");

            var offending = config.LoggingRules
                .Where(r => r.Targets.Any(t => t.Name == "csvFile"))
                .ToList();
            Assert.AreEqual(0, offending.Count, $"{name} still has a rule writing to csvFile");
        }

        /// <summary>
        /// Retention. NLog 6 marks archiveNumbering/archiveFileName legacy, and both archiveEvery and
        /// archiveFileName only work when fileName is static — ours carries ${shortdate}. Getting this
        /// wrong is silent: the daily files simply accumulate forever, which is the bug being fixed.
        /// </summary>
        [TestMethod]
        [DataRow("nlog.config")]
        [DataRow("nlog-Release.config")]
        [DataRow("nlog-Debug.config")]
        public void File_target_prunes_by_age_and_uses_no_legacy_archive_options(string name)
        {
            var file = Load(name).FindTargetByName<FileTarget>("txtFile");

            Assert.IsTrue(file.MaxArchiveDays > 0,
                $"{name}: maxArchiveDays is {file.MaxArchiveDays}, so nothing ever deletes the daily files");

            // 0 is not "unlimited" — it selects a handler that truncates the active log.
            Assert.IsTrue(file.MaxArchiveFiles > 0, $"{name}: maxArchiveFiles must not be 0");

            // ...and the count backstop must not undercut the day limit, or retention is silently
            // shorter than it reads.
            Assert.IsTrue(file.MaxArchiveFiles >= file.MaxArchiveDays,
                $"{name}: maxArchiveFiles ({file.MaxArchiveFiles}) would cap retention below "
                + $"maxArchiveDays ({file.MaxArchiveDays})");

            Assert.IsNull(file.ArchiveFileName,
                $"{name}: archiveFileName is legacy in NLog 6 and must not be combined with a ${{shortdate}} fileName");
            Assert.AreEqual(FileArchivePeriod.None, file.ArchiveEvery,
                $"{name}: archiveEvery only works with a static fileName");
        }

        /// <summary>
        /// The columns rescued from the deleted csv target. Losing them silently was the risk in
        /// dropping it.
        /// </summary>
        [TestMethod]
        [DataRow("nlog.config")]
        [DataRow("nlog-Release.config")]
        [DataRow("nlog-Debug.config")]
        public void File_layout_keeps_what_the_csv_target_used_to_carry(string name)
        {
            var layout = Load(name).FindTargetByName<FileTarget>("txtFile").Layout.ToString();

            foreach (var renderer in new[] { "callsite", "aspnet-request-url", "aspnet-mvc-action" })
                StringAssert.Contains(layout, renderer, $"{name}: layout dropped ${{{renderer}}}");

            // and the fields it always had
            foreach (var renderer in new[] { "longdate", "level", "logger", "message", "exception" })
                StringAssert.Contains(layout, renderer, $"{name}: layout dropped ${{{renderer}}}");
        }

        /// <summary>
        /// The "Now listening on: ..." lines. This rule sat *below* a Microsoft.* maxlevel="Info"
        /// final="true" blackhole, so it had been dead for as long as it has existed.
        /// </summary>
        [TestMethod]
        [DataRow("nlog.config")]
        [DataRow("nlog-Release.config")]
        [DataRow("nlog-Debug.config")]
        public void Hosting_lifetime_rule_comes_before_the_microsoft_blackhole(string name)
        {
            var rules = Load(name).LoggingRules;

            int lifetime = rules.ToList().FindIndex(r => r.LoggerNamePattern == "Microsoft.Hosting.Lifetime");
            int blackhole = rules.ToList().FindIndex(r => r.LoggerNamePattern == "Microsoft.*");

            Assert.IsTrue(lifetime >= 0, $"{name} has no Microsoft.Hosting.Lifetime rule");
            Assert.IsTrue(blackhole >= 0, $"{name} has no Microsoft.* rule");
            Assert.IsTrue(lifetime < blackhole,
                $"{name}: the lifetime rule is at {lifetime}, after the final=\"true\" blackhole at "
                + $"{blackhole}, so startup messages are swallowed");
        }

        /// <summary>
        /// internalLogLevel without a sink writes nowhere, which is exactly why the obsolete archive
        /// attributes these configs used to carry never announced themselves.
        /// </summary>
        [TestMethod]
        [DataRow("nlog.config")]
        [DataRow("nlog-Release.config")]
        [DataRow("nlog-Debug.config")]
        public void Internal_logging_has_somewhere_to_go(string name)
        {
            var xml = File.ReadAllText(ConfigPath(name));

            StringAssert.Contains(xml, "internalLogLevel", $"{name} sets no internalLogLevel");
            Assert.IsTrue(xml.Contains("internalLogToConsole") || xml.Contains("internalLogFile"),
                $"{name} sets internalLogLevel but gives it no sink, so warnings are discarded");
        }
    }
}
