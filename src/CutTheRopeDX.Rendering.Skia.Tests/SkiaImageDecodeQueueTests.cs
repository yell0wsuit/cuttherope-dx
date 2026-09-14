using System;
using System.Threading;
using System.Threading.Tasks;

using SkiaSharp;

using Xunit;

namespace CutTheRopeDX.Rendering.Skia.Tests
{
    public sealed class SkiaImageDecodeQueueTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

        [Fact]
        public void TakeHandsBackNothingForAnImageThatWasNeverPrepared()
        {
            using SkiaImageDecodeQueue queue = new(_ => Raster(4, 4), concurrency: 2);

            Assert.Null(queue.Take("images/never"));
        }

        [Fact]
        public void APreparedImageIsDecodedOffTheCallingThread()
        {
            int decodeThread = -1;
            using SkiaImageDecodeQueue queue = new(_ =>
            {
                decodeThread = Environment.CurrentManagedThreadId;
                return Raster(5, 9);
            }, concurrency: 2);

            queue.Prepare("images/test");

            // Waited for rather than taken straight away: a thread that blocks on a pool task that
            // has not started yet may run it itself, which is fine for the game but is not what
            // this test is about.
            Assert.True(SpinWait.SpinUntil(() => queue.IsReady("images/test"), Patience));
            using SKImage image = queue.Take("images/test");

            Assert.NotNull(image);
            Assert.Equal((5, 9), (image.Width, image.Height));
            Assert.NotEqual(Environment.CurrentManagedThreadId, decodeThread);
        }

        [Fact]
        public void TakeWaitsForADecodeThatIsStillRunning()
        {
            using ManualResetEventSlim release = new(false);
            using SkiaImageDecodeQueue queue = new(path =>
            {
                _ = release.Wait(Patience, TestContext.Current.CancellationToken);
                return Raster(3, 3);
            }, concurrency: 2);

            queue.Prepare("images/slow");
            Assert.False(queue.IsReady("images/slow"));

            _ = Task.Run(() =>
            {
                Thread.Sleep(50);
                release.Set();
            }, TestContext.Current.CancellationToken);
            using SKImage image = queue.Take("images/slow");

            Assert.NotNull(image);
        }

        [Fact]
        public void AnImageNothingIsDecodingNeverHoldsTheCallerUp()
        {
            using SkiaImageDecodeQueue queue = new(_ => Raster(2, 2), concurrency: 2);

            Assert.True(queue.IsReady("images/untouched"));
        }

        [Fact]
        public void AFinishedDecodeIsReportedReady()
        {
            using SkiaImageDecodeQueue queue = new(_ => Raster(2, 2), concurrency: 2);

            queue.Prepare("images/quick");

            Assert.True(SpinWait.SpinUntil(() => queue.IsReady("images/quick"), Patience));
            queue.Take("images/quick")?.Dispose();
        }

        [Fact]
        public void AFailedDecodeHandsBackNothingSoTheCallerCanLoadItInline()
        {
            using SkiaImageDecodeQueue queue = new(_ => throw new InvalidOperationException("corrupt"), concurrency: 2);

            queue.Prepare("images/broken");

            Assert.Null(queue.Take("images/broken"));
        }

        [Fact]
        public void DiscardingARunningDecodeReleasesItsImageOnceItFinishes()
        {
            using ManualResetEventSlim started = new(false);
            using ManualResetEventSlim release = new(false);
            SKImage produced = null;
            using SkiaImageDecodeQueue queue = new(path =>
            {
                started.Set();
                _ = release.Wait(Patience, TestContext.Current.CancellationToken);
                produced = Raster(6, 6);
                return produced;
            }, concurrency: 2);

            queue.Prepare("images/abandoned");
            Assert.True(started.Wait(Patience, TestContext.Current.CancellationToken));
            queue.Discard("images/abandoned");
            release.Set();

            Assert.True(SpinWait.SpinUntil(() => produced != null && produced.Handle == IntPtr.Zero, Patience));
            Assert.Null(queue.Take("images/abandoned"));
        }

        [Fact]
        public void DiscardingADecodeStillWaitingForASlotNeverRunsIt()
        {
            using ManualResetEventSlim blockerStarted = new(false);
            using ManualResetEventSlim release = new(false);
            System.Collections.Concurrent.ConcurrentBag<string> decoded = [];
            using SkiaImageDecodeQueue queue = new(path =>
            {
                decoded.Add(path);
                if (path == "images/blocker")
                {
                    blockerStarted.Set();
                    _ = release.Wait(Patience, TestContext.Current.CancellationToken);
                }

                return Raster(2, 2);
            }, concurrency: 1);

            queue.Prepare("images/blocker");
            Assert.True(blockerStarted.Wait(Patience, TestContext.Current.CancellationToken));
            queue.Prepare("images/scrolled-past");
            queue.Discard("images/scrolled-past");
            release.Set();

            queue.Take("images/blocker")?.Dispose();
            queue.Prepare("images/selected");
            Assert.True(SpinWait.SpinUntil(() => queue.IsReady("images/selected"), Patience));
            queue.Take("images/selected")?.Dispose();

            Assert.DoesNotContain("images/scrolled-past", decoded);
        }

        [Fact]
        public void ThePeakCountsDecodedPixelsUntilTheyAreTaken()
        {
            using SkiaImageDecodeQueue queue = new(_ => Raster(4, 4), concurrency: 2);

            queue.Prepare("images/first");
            queue.Prepare("images/second");
            Assert.True(SpinWait.SpinUntil(
                () => queue.IsReady("images/first") && queue.IsReady("images/second"), Patience));
            queue.Take("images/first")?.Dispose();

            Assert.Equal(2 * 4 * 4 * 4, queue.TakePeakPreparedPixelBytes());
            Assert.Equal(4 * 4 * 4, queue.TakePeakPreparedPixelBytes());

            queue.Take("images/second")?.Dispose();
            _ = queue.TakePeakPreparedPixelBytes();

            Assert.Equal(0, queue.TakePeakPreparedPixelBytes());
        }

        [Fact]
        public void PreparingTheSameImageTwiceDecodesItOnce()
        {
            int decodes = 0;
            using SkiaImageDecodeQueue queue = new(path =>
            {
                _ = Interlocked.Increment(ref decodes);
                return Raster(2, 2);
            }, concurrency: 2);

            queue.Prepare("images/twice");
            queue.Prepare("images/twice");
            queue.Take("images/twice")?.Dispose();

            Assert.Equal(1, decodes);
        }

        [Fact]
        public void DecodeRasterProducesPixelsWithoutAGraphicsDevice()
        {
            using SKImage source = Raster(7, 11);
            using SKData png = source.Encode(SKEncodedImageFormat.Png, 100);

            using SKImage decoded = SkiaImageDecodeQueue.DecodeRaster(png.ToArray());

            Assert.Equal((7, 11), (decoded.Width, decoded.Height));
            Assert.False(decoded.IsLazyGenerated);
            Assert.False(decoded.IsTextureBacked);
        }

        private static SKImage Raster(int width, int height)
        {
            using SKBitmap bitmap = new(width, height);
            bitmap.Erase(SKColors.Red);
            return SKImage.FromBitmap(bitmap);
        }
    }
}
