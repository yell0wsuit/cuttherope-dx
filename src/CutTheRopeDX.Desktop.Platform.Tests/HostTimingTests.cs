using System;
using System.Collections.Generic;

using Xunit;
namespace CutTheRopeDX.Desktop.Platform.Tests
{
    public sealed class HostTimingTests
    {
        [Theory]
        [InlineData(30)]
        [InlineData(60)]
        [InlineData(120)]
        [InlineData(144)]
        public void PresentationRateDoesNotChangeUpdates(int hz)
        {
            SdlHostLoop loop = new(); loop.Reset(TimeSpan.Zero);
            int updates = 0, draws = 0;
            for (int i = 1; i <= hz; i++)
            {
                _ = loop.Advance(TimeSpan.FromTicks(TimeSpan.TicksPerSecond * i / hz), delta => { Assert.Equal(16f, delta); updates++; }, () => draws++);
            }

            Assert.Equal(60, updates); Assert.Equal(Math.Min(hz, 60), draws);
        }
        [Fact]
        public void CatchupUpdatesPrecedeOneDrawAndStallIsBounded()
        {
            SdlHostLoop loop = new(); loop.Reset(TimeSpan.Zero); List<string> events = [];
            Assert.Equal(30, loop.Advance(TimeSpan.FromSeconds(4), _ => events.Add("update"), () => events.Add("draw")));
            Assert.Equal(31, events.Count); Assert.Equal("draw", events[^1]);
        }
        [Fact]
        public void SuspensionAndRecoveryDiscardDebt()
        {
            SdlHostLoop loop = new(); loop.Reset(TimeSpan.Zero); int updates = 0;
            _ = loop.Advance(TimeSpan.FromMilliseconds(10), _ => updates++, () => { });
            loop.SetSuspended(true, TimeSpan.FromMilliseconds(10));
            _ = loop.Advance(TimeSpan.FromSeconds(10), _ => updates++, () => { });
            loop.SetSuspended(false, TimeSpan.FromSeconds(10));
            _ = loop.Advance(TimeSpan.FromSeconds(10) + TimeSpan.FromTicks(166665), _ => updates++, () => { });
            Assert.Equal(0, updates);
            _ = loop.Advance(TimeSpan.FromSeconds(10) + TimeSpan.FromTicks(166666), _ => updates++, () => { });
            Assert.Equal(1, updates);
            loop.Reset(TimeSpan.FromSeconds(20));
            Assert.Equal(0, loop.Advance(TimeSpan.FromSeconds(20), _ => updates++, () => { }));
        }
    }
}
