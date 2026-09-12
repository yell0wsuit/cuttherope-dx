using System.IO;
using System.Reflection;

using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public class PlaytestHandshakeTests
    {
        [Fact]
        public void AnnounceWritesOnlyTheHandshakeLine()
        {
            using StringWriter writer = new();
            Assembly assembly = typeof(PlaytestHandshake).Assembly;
            string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString() ?? "";

            string announced = PlaytestHandshake.Announce(writer);

            Assert.Equal(PlaytestHandshake.FormatLine(version) + writer.NewLine, writer.ToString());

            // What is returned has to be what was written: the caller logs it, and a version
            // resolved a second time from a different assembly would not match.
            Assert.Equal(PlaytestHandshake.FormatLine(version), announced);
        }

        [Fact]
        public void FormatLineIncludesSignatureProtocolAndVersion()
        {
            string line = PlaytestHandshake.FormatLine("1.2.3");

            Assert.Equal("ctrdx-playtest 1 1.2.3", line);
        }

        [Fact]
        public void FormatLineBlankVersionRendersUnknown()
        {
            Assert.Equal("ctrdx-playtest 1 unknown", PlaytestHandshake.FormatLine("   "));
        }

        [Fact]
        public void FormatLineTrimsSurroundingWhitespace()
        {
            Assert.Equal("ctrdx-playtest 1 1.0.0", PlaytestHandshake.FormatLine("  1.0.0  "));
        }

        [Fact]
        public void SignatureIsStableContract()
        {
            // The editor keys off this exact token to recognize Cut the Rope: DX; keep it stable.
            Assert.Equal("ctrdx-playtest", PlaytestHandshake.Signature);
            Assert.StartsWith(PlaytestHandshake.Signature + " ", PlaytestHandshake.FormatLine("1.0.0"));
        }
    }
}
