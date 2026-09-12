using System;

using CutTheRopeDX.Framework.Diagnostics;

using Microsoft.Extensions.Logging;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// What a log may not carry out of the machine that wrote it.
    /// </summary>
    /// <remarks>
    /// A log is written to be sent to someone else, and almost every path in one runs through the
    /// home directory: the save directory, the content directory, and every stack frame of every
    /// attached exception. The account name in the middle of those is what says who sent the file,
    /// so taking it out is the whole point rather than a nicety.
    /// </remarks>
    public sealed class LogRedactionTests
    {
        private static string Account => Environment.UserName;

        [Fact]
        public void TheAccountNameIsTakenOutOfAPath()
        {
            string redacted = LogEntryFormat.Redact($@"C:\Users\{Account}\Saved Games\ctrdx");

            Assert.DoesNotContain(Account, redacted, StringComparison.Ordinal);
            Assert.Contains(LogEntryFormat.RedactedUserName, redacted, StringComparison.Ordinal);
        }

        [Fact]
        public void TheAccountNameIsTakenOutWhateverCaseItArrivesIn()
        {
            // Windows paths are case-insensitive and nothing normalises them on the way in. SDL,
            // FFmpeg and the environment all hand back whatever case they were given, so an exact
            // match leaves the name in the file for anyone who typed it differently.
            string redacted = LogEntryFormat.Redact($@"C:\USERS\{Account.ToUpperInvariant()}\ctrdx");

            Assert.DoesNotContain(Account, redacted, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void TheShortPathAliasOfTheAccountNameIsTakenOutToo()
        {
            // GetTempPath returns the 8.3 form on any machine whose TEMP is set that way, and the
            // alias still names the person: yell0wsuit shortens to YELL0W~1.
            string alias = Account.Length >= 6
                ? Account[..6].ToUpperInvariant() + "~1"
                : Account.ToUpperInvariant() + "~1";

            string redacted = LogEntryFormat.Redact($@"C:\Users\{alias}\AppData\Local\Temp");

            Assert.DoesNotContain(alias, redacted, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AComposedEntryIsRedactedThroughItsException()
        {
            Exception failure = new InvalidOperationException($@"cannot open C:\Users\{Account}\ctrdx.sav");

            string line = LogEntryFormat.Compose(
                LogLevel.Error, "ctrdx.preferences", $@"saving to C:\Users\{Account}", failure);

            Assert.DoesNotContain(Account, line, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void TextWithoutTheAccountNameIsLeftAlone()
        {
            const string ordinary = "Renderer Vulkan, audio ready";

            Assert.Equal(ordinary, LogEntryFormat.Redact(ordinary));
        }
    }
}
