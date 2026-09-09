using System;
using System.IO;

using Microsoft.Extensions.Logging;

using Xunit;

namespace CutTheRopeDX.Desktop.Tests
{
    public class LoggingSetupTests
    {
        /// <summary>The stamp every test that needs a known file name builds its log with.</summary>
        private static readonly DateTime Stamp = new(2026, 9, 9, 11, 30, 0, DateTimeKind.Local);

        private static string LogPath(string root, DateTime stamp)
        {
            return Path.Combine(root, "logs", LoggingSetup.LogFileName(stamp, fallback: false));
        }

        [Fact]
        public void EachRunWritesItsOwnFileAndLeavesEarlierRunsAlone()
        {
            string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _ = Directory.CreateDirectory(Path.Combine(root, "logs"));
            string earlier = LogPath(root, Stamp.AddDays(-1));
            File.WriteAllText(earlier, "earlier history");
            try
            {
                using ILoggerFactory factory = LoggingSetup.Create(root, null, Stamp);
                factory.CreateLogger("Sdl.Host").Log(LogLevel.Information, default, "new session", null,
                    static (message, _) => message);
                factory.Dispose();

                Assert.Contains("new session", File.ReadAllText(LogPath(root, Stamp)));
                Assert.Equal("earlier history", File.ReadAllText(earlier));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void OldRunsArePrunedSoTheDirectoryStaysBounded()
        {
            string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string directory = Path.Combine(root, "logs");
            _ = Directory.CreateDirectory(directory);
            DateTime newest = Stamp.AddHours(-1);
            for (int age = 0; age < 15; age++)
            {
                string path = LogPath(root, newest.AddHours(-age));
                File.WriteAllText(path, "run history");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-age));
            }

            try
            {
                using ILoggerFactory factory = LoggingSetup.Create(root, null, Stamp);
                factory.Dispose();

                // Nine survivors plus this run's own file.
                Assert.Equal(10, Directory.GetFiles(directory, "ctrdx-*.log").Length);
                Assert.True(File.Exists(LogPath(root, Stamp)));
                Assert.True(File.Exists(LogPath(root, newest)));
                Assert.False(File.Exists(LogPath(root, newest.AddHours(-9))));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void EveryLogOpensWithTheBuildItCameFrom()
        {
            string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _ = Directory.CreateDirectory(root);
            try
            {
                using ILoggerFactory factory = LoggingSetup.Create(root, null, Stamp);
                factory.CreateLogger("Sdl.Host").Log(LogLevel.Information, default, "after the banner", null,
                    static (message, _) => message);
                factory.Dispose();

                string[] lines = File.ReadAllLines(LogPath(root, Stamp));

                Assert.Equal("Cut The Rope: DX", lines[0]);
                Assert.EndsWith(" version", lines[1]);
                Assert.StartsWith("Version: ", lines[2]);
                Assert.Contains(lines, line => line.Contains("after the banner", StringComparison.Ordinal));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Theory]
        [InlineData(LogLevel.Trace, LogLevel.Trace, true)]
        [InlineData(LogLevel.Error, LogLevel.Warning, false)]
        [InlineData(LogLevel.Error, LogLevel.Error, true)]
        public void RequestedLevelAppliesToBothSinks(LogLevel requested, LogLevel emitted, bool expected)
        {
            string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using StringWriter output = new();
            using StringWriter error = new();
            TextWriter previousOutput = Console.Out;
            TextWriter previousError = Console.Error;
            Console.SetOut(output);
            Console.SetError(error);
            try
            {
                using ILoggerFactory factory = LoggingSetup.Create(root, requested, Stamp);
                factory.CreateLogger("Sdl.Host").Log(emitted, default, "level marker", null,
                    static (message, _) => message);
                factory.Dispose();

                string contents = File.ReadAllText(LogPath(root, Stamp));
                Assert.Equal(expected, contents.Contains("level marker", StringComparison.Ordinal));
                Assert.Equal(expected && emitted < LogLevel.Error,
                    output.ToString().Contains("level marker", StringComparison.Ordinal));
                Assert.Equal(expected && emitted >= LogLevel.Error,
                    error.ToString().Contains("level marker", StringComparison.Ordinal));
            }
            finally
            {
                Console.SetOut(previousOutput);
                Console.SetError(previousError);
                Directory.Delete(root, recursive: true);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AnUnusableLogDirectoryOrFallbackDoesNotPreventStartup(bool blockFallback)
        {
            string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _ = Directory.CreateDirectory(root);
            try
            {
                if (blockFallback)
                {
                    _ = Directory.CreateDirectory(LogPath(root, Stamp));
                    _ = Directory.CreateDirectory(
                        Path.Combine(root, "logs", LoggingSetup.LogFileName(Stamp, fallback: true)));
                }
                else
                {
                    File.WriteAllText(Path.Combine(root, "logs"), "blocking file");
                }

                using ILoggerFactory factory = LoggingSetup.Create(root, null, Stamp);
                factory.CreateLogger("Sdl.Host").Log(LogLevel.Warning, default, "console only", null,
                    static (message, _) => message);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void NoSwitchMeansNoRequestedLevel()
        {
            Assert.Null(LoggingSetup.ParseLevel(["--renderer", "vulkan"]));
        }

        [Fact]
        public void ParsesEachAcceptedLevel()
        {
            Assert.Equal(LogLevel.Trace, LoggingSetup.ParseLevel(["--log-level", "trace"]));
            Assert.Equal(LogLevel.Debug, LoggingSetup.ParseLevel(["--log-level", "debug"]));
            Assert.Equal(LogLevel.Information, LoggingSetup.ParseLevel(["--log-level", "info"]));
            Assert.Equal(LogLevel.Warning, LoggingSetup.ParseLevel(["--log-level", "warn"]));
            Assert.Equal(LogLevel.Error, LoggingSetup.ParseLevel(["--log-level", "error"]));
        }

        [Fact]
        public void ParsingIsCaseInsensitive()
        {
            Assert.Equal(LogLevel.Warning, LoggingSetup.ParseLevel(["--log-level", "WARN"]));
        }

        [Fact]
        public void AnUnknownLevelIsRejectedByName()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => LoggingSetup.ParseLevel(["--log-level", "chatty"]));

            Assert.Contains("chatty", error.Message);
        }

        // NReco's LogMessage is a struct of readonly fields with no public constructor, so a
        // test cannot build one. FormatEntry projects its fields onto Compose, and Compose is
        // what carries the behavior worth asserting.
        [Fact]
        public void ComposePutsTheExceptionOnItsOwnLine()
        {
            string formatted = LoggingSetup.Compose(
                LogLevel.Critical,
                "Sdl.Host",
                "device lost",
                new InvalidOperationException("no drawable"));

            Assert.Contains("device lost", formatted);
            Assert.Contains(Environment.NewLine + "System.InvalidOperationException", formatted);
        }

        [Fact]
        public void ComposeBracketsTheLevelAndSeparatesFieldsWithSpaces()
        {
            string formatted = LoggingSetup.Compose(LogLevel.Information, "Sdl.Host", "renderer=Metal", null);

            Assert.DoesNotContain("\t", formatted);
            Assert.Contains(" [Information] Sdl.Host renderer=Metal", formatted);
        }

        [Fact]
        public void ComposeKeepsTheAccountNameOutOfTheLog()
        {
            string account = Environment.UserName;
            Assert.True(account.Length >= 3, "This machine's account name is too short to redact.");

            string formatted = LoggingSetup.Compose(
                LogLevel.Information,
                "Preferences",
                $"Using save directory: /Users/{account}/Documents/save",
                null);

            Assert.DoesNotContain(account, formatted);
            Assert.Contains("/Users/[redactedUsername]/Documents/save", formatted);
        }

        [Fact]
        public void ComposeOmitsTheExceptionBlockWhenThereIsNone()
        {
            Assert.DoesNotContain(
                Environment.NewLine,
                LoggingSetup.Compose(LogLevel.Information, "Sdl.Host", "renderer=Metal", null));
        }

        [Fact]
        public void AnUnopenableLogFallsBackInsteadOfThrowing()
        {
            string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _ = Directory.CreateDirectory(root);

            // A directory sitting where the log file belongs cannot be opened as a file.
            _ = Directory.CreateDirectory(LogPath(root, Stamp));
            try
            {
                using ILoggerFactory factory = LoggingSetup.Create(root, null, Stamp);
                factory.CreateLogger("Sdl.Host").Log(LogLevel.Warning, default, "recovered", null, static (message, _) => message);
                factory.Dispose();

                string fallback = Path.Combine(root, "logs", LoggingSetup.LogFileName(Stamp, fallback: true));
                Assert.True(File.Exists(fallback));
                Assert.Contains("recovered", File.ReadAllText(fallback));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CreateWritesIntoALogsDirectoryUnderTheSaveDirectory()
        {
            string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _ = Directory.CreateDirectory(root);
            try
            {
                using ILoggerFactory factory = LoggingSetup.Create(root, null, Stamp);
                factory.CreateLogger("Sdl.Host").Log(LogLevel.Warning, default, "written", null, static (message, _) => message);
                factory.Dispose();

                string path = LogPath(root, Stamp);
                Assert.True(File.Exists(path));
                Assert.Contains("written", File.ReadAllText(path));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void InformationReachesBothSinksByDefault()
        {
            string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _ = Directory.CreateDirectory(root);
            StringWriter captured = new();
            TextWriter previous = Console.Out;
            Console.SetOut(captured);
            try
            {
                using ILoggerFactory factory = LoggingSetup.Create(root, null, Stamp);
                factory.CreateLogger("Sdl.Host").Log(LogLevel.Information, default, "quiet", null, static (message, _) => message);
                factory.Dispose();

                Assert.Contains("quiet", File.ReadAllText(LogPath(root, Stamp)));
                Assert.Contains("quiet", captured.ToString());
            }
            finally
            {
                Console.SetOut(previous);
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
