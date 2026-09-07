using System;

using CutTheRopeDX.Desktop.Platform.Audio;

using Xunit;

namespace CutTheRopeDX.Desktop.Platform.Tests
{
    /// <summary>
    /// Covers the PCM output a movie's soundtrack is pushed through, against a real SDL audio
    /// stream that is not bound to a device, so queue accounting is measured rather than mocked.
    /// </summary>
    public sealed class PcmStreamTests : IDisposable
    {
        private const int Frequency = 44100;
        private const int Channels = 2;
        private const int BytesPerFrame = Channels * 2;

        private readonly SdlPcmStream stream =
            SdlPcmStream.CreateForTesting(Frequency, Channels)
            ?? throw new InvalidOperationException("Could not create an unbound audio stream.");

        /// <summary>Builds <paramref name="milliseconds"/> of silent interleaved 16-bit PCM.</summary>
        private static byte[] Pcm(int milliseconds)
        {
            return new byte[Frequency * milliseconds / 1000 * BytesPerFrame];
        }

        [Fact]
        public void AFreshStreamHasNothingQueued()
        {
            Assert.Equal(0, stream.QueuedFrames);
            Assert.Equal(TimeSpan.Zero, stream.Queued);
            Assert.True(stream.IsDrained);
        }

        [Fact]
        public void SubmittedAudioIsCountedInFramesAndTime()
        {
            stream.Submit(Pcm(100));

            Assert.Equal(Frequency / 10, stream.QueuedFrames);
            Assert.Equal(100, Math.Round(stream.Queued.TotalMilliseconds));
            Assert.False(stream.IsDrained);
        }

        [Fact]
        public void QueueDepthIsWhatBoundsFurtherSubmissions()
        {
            TimeSpan bound = TimeSpan.FromMilliseconds(200);
            Assert.True(stream.HasRoomFor(bound));

            stream.Submit(Pcm(150));
            Assert.True(stream.HasRoomFor(bound));

            stream.Submit(Pcm(100));
            Assert.False(stream.HasRoomFor(bound));
        }

        [Fact]
        public void ConsumedAudioLeavesTheQueueAndTheStreamDrains()
        {
            stream.Submit(Pcm(50));

            int consumed = stream.DrainForTesting(new byte[Pcm(50).Length]);

            Assert.Equal(Pcm(50).Length, consumed);
            Assert.Equal(0, stream.QueuedFrames);
            Assert.True(stream.IsDrained);
        }

        [Fact]
        public void StoppingDiscardsAudioThatWasQueuedButNeverHeard()
        {
            // Skipping a cutscene has to silence it at once; anything still queued would otherwise
            // keep playing over whatever screen comes next.
            stream.Submit(Pcm(500));

            stream.Stop();

            Assert.Equal(0, stream.QueuedFrames);
            Assert.True(stream.IsDrained);
        }

        [Fact]
        public void AStoppedStreamAcceptsAudioAgain()
        {
            stream.Submit(Pcm(100));
            stream.Stop();

            stream.Submit(Pcm(30));

            Assert.Equal(Frequency * 30 / 1000, stream.QueuedFrames);
        }

        [Fact]
        public void PausingDoesNotDiscardWhatIsQueued()
        {
            stream.Submit(Pcm(100));

            stream.Pause();

            Assert.Equal(Frequency / 10, stream.QueuedFrames);

            stream.Resume();

            Assert.Equal(Frequency / 10, stream.QueuedFrames);
        }

        [Fact]
        public void SubmittingNothingIsHarmless()
        {
            stream.Submit([]);

            Assert.True(stream.IsDrained);
        }

        [Fact]
        public void ADisposedStreamRefusesFurtherAudioRatherThanUsingAFreedHandle()
        {
            stream.Dispose();

            _ = Assert.Throws<ObjectDisposedException>(() => stream.Submit(Pcm(10)));
        }

        [Fact]
        public void DisposingTwiceIsHarmless()
        {
            stream.Dispose();
            stream.Dispose();
        }

        public void Dispose()
        {
            stream.Dispose();
        }
    }
}
