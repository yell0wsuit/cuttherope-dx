using System;
using System.Linq;

using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Tests;

using Microsoft.Extensions.Logging;

using Xunit;

namespace CutTheRopeDX.Desktop.Tests
{
    /// <summary>
    /// Covers the FFmpeg player's own state around a cutscene, without the native libraries: what
    /// it remembers between cutscenes decides whether the next one can finish.
    /// </summary>
    public sealed class VideoPlayerFFmpegStateTests : IDisposable
    {
        private readonly RecordingLoggerProvider recorder = new();
        private readonly ILoggerFactory factory;
        private readonly VideoPlayerFFmpeg player;
        private int finished;

        public VideoPlayerFFmpegStateTests()
        {
            factory = LoggerFactory.Create(builder =>
            {
                _ = builder.SetMinimumLevel(LogLevel.Trace);
                _ = builder.AddProvider(recorder);
            });
            Log.Factory = factory;

            // No library path, so nothing native is loaded and every movie is reported missing.
            player = new VideoPlayerFFmpeg(_ => false, _ => null);
            player.PlaybackFinished += () => finished++;
        }

        [Fact]
        public void APauseWithNothingOpenIsNotCarriedIntoTheNextCutscene()
        {
            // The host pauses on every focus loss. A pause remembered from the menu held the next
            // cutscene short of finishing, so it played to its last frame and sat there, black,
            // until a click resumed it.
            player.Pause();

            Assert.False(player.IsPaused);
            Assert.Contains(recorder.Records, record =>
                record.Category == LogCategories.MediaFFmpeg
                && record.Level == LogLevel.Debug
                && record.Message.Contains("no movie open", StringComparison.Ordinal));
        }

        [Fact]
        public void StoppingWithNothingOpenReportsNoCompletion()
        {
            player.Stop();

            Assert.Equal(0, finished);
        }

        [Fact]
        public void AMissingMovieFinishesOnceAndLeavesNothingPaused()
        {
            player.Pause();

            player.Play("ctr_intro", mute: true);
            player.Update();

            Assert.Equal(1, finished);
            Assert.False(player.IsPaused);
            Assert.False(player.IsPlaying());
            _ = Assert.Single(recorder.Records, record => record.Level == LogLevel.Warning);
        }

        [Fact]
        public void EveryLifecycleLineStaysBelowInformation()
        {
            player.Pause();
            player.Resume();
            player.Stop();
            player.Update();

            Assert.All(recorder.Records.Where(record => record.Category == LogCategories.MediaFFmpeg),
                record => Assert.True(record.Level < LogLevel.Information, record.Message));
        }

        public void Dispose()
        {
            player.Dispose();
            Log.Factory = null;
            factory.Dispose();
        }
    }
}
