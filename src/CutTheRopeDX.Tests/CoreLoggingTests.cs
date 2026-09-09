using System;
using System.IO;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Diagnostics;

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
