using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

using CutTheRopeDX.Browser;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public class ParallelPumpTests
    {
        [Fact]
        public async Task EveryItemIsHandedOutExactlyOnce()
        {
            string[] work = [.. Enumerable.Range(0, 500).Select(i => $"asset-{i}")];
            ConcurrentBag<string> handled = [];

            await ParallelPump.RunAsync(
                work,
                concurrency: 8,
                async path =>
                {
                    // Yielding is what lets the workers interleave, which is the only
                    // condition under which a shared cursor can hand the same index out
                    // twice or skip one entirely.
                    await Task.Yield();
                    handled.Add(path);
                },
                static _ => { });

            Assert.Equal(work.Length, handled.Count);
            Assert.Equal(work.Length, handled.Distinct().Count());
        }

        [Fact]
        public async Task ProgressCountsEveryCompletedItem()
        {
            string[] work = [.. Enumerable.Range(0, 500).Select(i => $"asset-{i}")];

            // Workers report progress from whichever thread finished their item, so several
            // callbacks can be in flight at once. Collecting the reported counts instead of
            // folding them into a shared variable keeps the assertion free of the same race
            // it is meant to catch, and checks more: every count arrives exactly once.
            ConcurrentBag<int> reported = [];

            await ParallelPump.RunAsync(
                work,
                concurrency: 8,
                static async _ => await Task.Yield(),
                reported.Add);

            Assert.Equal(
                Enumerable.Range(1, work.Length),
                reported.OrderBy(static count => count));
        }
    }
}
