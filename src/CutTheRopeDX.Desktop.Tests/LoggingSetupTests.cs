using System;
using System.IO;

using Microsoft.Extensions.Logging;

using Xunit;

namespace CutTheRopeDX.Desktop.Tests
{
    public class LoggingSetupTests
    {
        [Fact]
        public void APreviousProcessFallbackDoesNotReplaceThePrimaryLog()
        {
            string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string directory = Path.Combine(root, "logs");
            _ = Directory.CreateDirectory(directory);
            string primary = Path.Combine(directory, "ctrdx.log");
            string fallback = Path.Combine(directory, "ctrdx-12345.log");
            File.WriteAllText(primary, "primary history");
            File.WriteAllText(fallback, "fallback history");
            File.SetLastWriteTimeUtc(primary, DateTime.UtcNow.AddDays(-1));
            try
            {
                using ILoggerFactory factory = LoggingSetup.Create(root, null);
                factory.CreateLogger("Sdl.Host").Log(LogLevel.Information, default, "new session", null,
                    static (message, _) => message);
                factory.Dispose();

                Assert.Contains("new session", File.ReadAllText(primary));
                Assert.Equal("fallback history", File.ReadAllText(fallback));
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
                using ILoggerFactory factory = LoggingSetup.Create(root, requested);
                factory.CreateLogger("Sdl.Host").Log(emitted, default, "level marker", null,
                    static (message, _) => message);
                factory.Dispose();

                string contents = File.ReadAllText(Path.Combine(root, "logs", "ctrdx.log"));
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
                    _ = Directory.CreateDirectory(Path.Combine(root, "logs", "ctrdx.log"));
                    _ = Directory.CreateDirectory(Path.Combine(root, "logs", $"ctrdx-{Environment.ProcessId}.log"));
                }
                else
                {
                    File.WriteAllText(Path.Combine(root, "logs"), "blocking file");
                }

                using ILoggerFactory factory = LoggingSetup.Create(root, null);
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
            _ = Directory.CreateDirectory(Path.Combine(root, "logs", "ctrdx.log"));
            try
            {
                using ILoggerFactory factory = LoggingSetup.Create(root, null);
                factory.CreateLogger("Sdl.Host").Log(LogLevel.Warning, default, "recovered", null, static (message, _) => message);
                factory.Dispose();

                string fallback = Path.Combine(root, "logs", $"ctrdx-{Environment.ProcessId}.log");
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
                using ILoggerFactory factory = LoggingSetup.Create(root, null);
                factory.CreateLogger("Sdl.Host").Log(LogLevel.Warning, default, "written", null, static (message, _) => message);
                factory.Dispose();

                string path = Path.Combine(root, "logs", "ctrdx.log");
                Assert.True(File.Exists(path));
                Assert.Contains("written", File.ReadAllText(path));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void InformationReachesTheFileButNotTheConsoleByDefault()
        {
            string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _ = Directory.CreateDirectory(root);
            StringWriter captured = new();
            TextWriter previous = Console.Out;
            Console.SetOut(captured);
            try
            {
                using ILoggerFactory factory = LoggingSetup.Create(root, null);
                factory.CreateLogger("Sdl.Host").Log(LogLevel.Information, default, "quiet", null, static (message, _) => message);
                factory.Dispose();

                Assert.Contains("quiet", File.ReadAllText(Path.Combine(root, "logs", "ctrdx.log")));
                Assert.DoesNotContain("quiet", captured.ToString());
            }
            finally
            {
                Console.SetOut(previous);
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
