using System;
using System.IO;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Helpers;

using Microsoft.Extensions.Logging;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public class CoreLoggingTests
    {
        [Fact]
        public void ARotatedAtlasFrameWarnsUnderItsOwnCategory()
        {
            RecordingLoggerProvider recorder = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
            Log.Factory = factory;
            try
            {
                const string json =
                    """{"frames":{"spin":{"frame":{"x":0,"y":0,"w":4,"h":4},"rotated":true}}}""";

                _ = TexturePackerAtlasParser.Parse(json, null);

                LogRecord entry = Assert.Single(recorder.Records);
                Assert.Equal(LogCategories.ContentAtlas, entry.Category);
                Assert.Equal(LogLevel.Warning, entry.Level);
                Assert.Contains("spin", entry.Message);
            }
            finally
            {
                Log.Factory = null;
            }
        }

        [Fact]
        public void AFailedPreferenceSaveLogsTheExceptionAtError()
        {
            RecordingLoggerProvider recorder = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
            Log.Factory = factory;
            try
            {
                using UnwritableStream stream = new();

                Assert.False(Preferences.SaveToStream(stream));

                LogRecord entry = Assert.Single(recorder.Records, record =>
                    record.Category == LogCategories.Preferences && record.Level == LogLevel.Error);
                Assert.NotNull(entry.Exception);
            }
            finally
            {
                Log.Factory = null;
            }
        }

        [Fact]
        public void UnreadableXmlContentIsReportedInsteadOfReturningABareNull()
        {
            string relativePath = Path.Combine("test-data", $"{Guid.NewGuid():N}.xml");
            string fullPath = Path.Combine(ContentPaths.GetContentRootAbsolute(), relativePath);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, "<map><object></map>");

            RecordingLoggerProvider recorder = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
            Log.Factory = factory;
            try
            {
                Assert.Null(ContentPaths.LoadXml(relativePath));

                LogRecord entry = Assert.Single(recorder.Records);
                Assert.Equal(LogCategories.ContentXml, entry.Category);
                Assert.Equal(LogLevel.Error, entry.Level);
                Assert.Contains(relativePath, entry.Message);
                Assert.NotNull(entry.Exception);
            }
            finally
            {
                Log.Factory = null;
                File.Delete(fullPath);
            }
        }

        /// <summary>
        /// The editor reads level errors straight off the standard error pipe, so what lands
        /// there carries no timestamp, level or category. The log gets its own copy; what must
        /// never happen is a decorated line reaching the pipe.
        /// </summary>
        [Fact]
        public void ACustomLevelFailureStaysBareOnStandardError()
        {
            _ = HeadlessGame.Boot();
            GameController controller = HeadlessGame.LoadLevelWithController(1, 4);
            GameScene scene = (GameScene)controller.GetView(0).GetChild(0);

            RecordingLoggerProvider recorder = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
            Log.Factory = factory;
            StringWriter captured = new();
            TextWriter previous = Console.Error;
            Console.SetError(captured);
            try
            {
                CustomLevelSession.Activate(Path.Combine(Path.GetTempPath(), "does-not-exist.xml"));

                scene.Reload();

                string written = captured.ToString();
                Assert.NotEqual(string.Empty, written);

                // The editor matches these bytes, so the line has to be the reason and nothing
                // else: no bracketed level, no category, and no timestamp in front of it.
                Assert.DoesNotContain("[", written);
                Assert.DoesNotContain(LogCategories.Playtest, written);

                LogRecord entry = Assert.Single(recorder.Records);
                Assert.Equal(LogCategories.Playtest, entry.Category);
                Assert.Equal(LogLevel.Warning, entry.Level);

                // The logged copy is the same reason with a prefix, so what reached the pipe is
                // exactly its tail. Asserting the relationship rather than the wording keeps this
                // honest if the reason is ever reworded.
                Assert.EndsWith(written.TrimEnd(), entry.Message, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetError(previous);
                Log.Factory = null;
                CustomLevelSession.Clear();
            }
        }
    }

    /// <summary>A stream that refuses every write, standing in for a full or read-only disk.</summary>
    internal sealed class UnwritableStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => 0;

        public override long Position { get => 0; set { } }

        public override void Flush()
        {
            throw new IOException("no space");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new IOException("no space");
        }
    }
}
