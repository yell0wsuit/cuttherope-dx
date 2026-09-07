using System;
using System.Collections.Generic;

using CutTheRopeDX.Desktop.Platform.Graphics;

using Xunit;

namespace CutTheRopeDX.Desktop.Platform.Tests
{
    public sealed class GraphicsRecoveryTests
    {
        [Theory]
        [InlineData("macos", GraphicsBackendKind.Metal, GraphicsBackendKind.Metal, GraphicsBackendKind.OpenGL)]
        [InlineData("macos", GraphicsBackendKind.OpenGL, GraphicsBackendKind.OpenGL, GraphicsBackendKind.Metal)]
        [InlineData("linux", GraphicsBackendKind.OpenGL, GraphicsBackendKind.OpenGL, GraphicsBackendKind.Vulkan)]
        public void TheLostRendererIsTriedFirstAndThenTheRest(
            string platform, GraphicsBackendKind lost, params GraphicsBackendKind[] expected)
        {
            GraphicsRecoveryCoordinator coordinator = new(platform, null);

            Assert.Equal(expected, coordinator.OrderAfter(lost));
        }

        [Fact]
        public void EveryPlatformCandidateStaysReachableAfterALoss()
        {
            GraphicsRecoveryCoordinator coordinator = new("windows", null);

            Assert.Equal(
                [GraphicsBackendKind.OpenGL, GraphicsBackendKind.Vulkan, GraphicsBackendKind.Angle],
                coordinator.OrderAfter(GraphicsBackendKind.OpenGL));
        }

        [Fact]
        public void AForcedRendererIsRetriedAndNothingElseIsTried()
        {
            GraphicsRecoveryCoordinator coordinator = new("macos", GraphicsBackendKind.OpenGL);
            List<string> events = [];
            using GraphicsSelection<Resource> lost = Select("macos", GraphicsBackendKind.OpenGL, events);
            List<GraphicsBackendKind> attempts = [];

            GraphicsRecoveryFailedException failure = Assert.Throws<GraphicsRecoveryFailedException>(
                () => coordinator.Recover<Resource>(lost, (kind, _) =>
                {
                    attempts.Add(kind);
                    throw new InvalidOperationException(kind.ToString());
                }, _ => { }));

            Assert.Equal([GraphicsBackendKind.OpenGL], attempts);
            Assert.Equal([GraphicsBackendKind.OpenGL], failure.Attempted);
            Assert.DoesNotContain(GraphicsBackendKind.Metal, attempts);
            Assert.Equal(0, coordinator.Recoveries);
        }

        [Fact]
        public void AForcedRendererThatComesBackIsKept()
        {
            GraphicsRecoveryCoordinator coordinator = new("macos", GraphicsBackendKind.OpenGL);
            List<string> events = [];
            using GraphicsSelection<Resource> lost = Select("macos", GraphicsBackendKind.OpenGL, events);

            using GraphicsSelection<Resource> replacement = coordinator.Recover(lost,
                (kind, lifetime) => lifetime.Own(new Resource("replacement " + kind, events)),
                _ => { });

            Assert.Equal(GraphicsBackendKind.OpenGL, replacement.Kind);
            Assert.Equal(1, coordinator.Recoveries);
        }

        [Fact]
        public void TheLostDeviceIsReleasedBeforeAReplacementIsBuilt()
        {
            GraphicsRecoveryCoordinator coordinator = new("macos", null);
            List<string> events = [];
            GraphicsSelection<Resource> lost = Select("macos", null, events);

            using GraphicsSelection<Resource> replacement = coordinator.Recover(lost, (kind, lifetime) =>
            {
                events.Add("create " + kind);
                return lifetime.Own(new Resource("replacement", events));
            }, _ => { });

            Assert.Equal(
                ["dispose Metal device", "dispose Metal window", "create Metal"],
                events);
        }

        [Fact]
        public void APartlyBuiltReplacementIsUnwoundBeforeTheNextCandidate()
        {
            GraphicsRecoveryCoordinator coordinator = new("macos", null);
            List<string> events = [];
            GraphicsSelection<Resource> lost = Select("macos", null, events);
            events.Clear();

            using GraphicsSelection<Resource> replacement = coordinator.Recover(lost, (kind, lifetime) =>
            {
                if (kind == GraphicsBackendKind.Metal)
                {
                    _ = lifetime.Own(new Resource("window", events));
                    _ = lifetime.Own(new Resource("context", events));
                    throw new InvalidOperationException("surface creation failed");
                }

                Assert.Equal(
                    ["dispose Metal device", "dispose Metal window", "dispose context", "dispose window"],
                    events);
                return lifetime.Own(new Resource("GL", events));
            }, _ => { });

            Assert.Equal(GraphicsBackendKind.OpenGL, replacement.Kind);
        }

        [Fact]
        public void ATeardownThatFailsStillGetsARunningDevice()
        {
            GraphicsRecoveryCoordinator coordinator = new("macos", null);
            List<string> events = [];
            GraphicsSelection<Resource> lost = BackendSelector.Select("macos", GraphicsBackendKind.Metal,
                (kind, lifetime) => lifetime.Own(
                    new Resource("device", events, new InvalidOperationException("teardown failed"))),
                _ => { });

            using GraphicsSelection<Resource> replacement = coordinator.Recover(lost,
                (kind, lifetime) => lifetime.Own(new Resource("replacement", events)),
                _ => { });

            Assert.Equal(GraphicsBackendKind.Metal, replacement.Kind);
            Assert.Equal(1, coordinator.Recoveries);
        }

        [Fact]
        public void ATeardownFailureIsReportedWhenNothingReplacesTheDevice()
        {
            GraphicsRecoveryCoordinator coordinator = new("macos", null);
            List<string> events = [];
            InvalidOperationException teardown = new("teardown failed");
            InvalidOperationException creation = new("no device");
            GraphicsSelection<Resource> lost = BackendSelector.Select("macos", GraphicsBackendKind.Metal,
                (kind, lifetime) => lifetime.Own(new Resource("device", events, teardown)),
                _ => { });

            GraphicsRecoveryFailedException failure = Assert.Throws<GraphicsRecoveryFailedException>(
                () => coordinator.Recover<Resource>(lost, (_, _) => throw creation, _ => { }));

            IReadOnlyList<Exception> reported = ((AggregateException)failure.InnerException).Flatten().InnerExceptions;
            Assert.Contains(creation, reported);
            Assert.Contains(teardown, reported);
        }

        [Fact]
        public void RepeatedLossesLeaveEveryDeviceDisposedExactlyOnce()
        {
            const int Cycles = 20;
            GraphicsRecoveryCoordinator coordinator = new("macos", null);
            List<string> events = [];
            GraphicsSelection<Resource> live = Select("macos", null, events);

            for (int cycle = 0; cycle < Cycles; cycle++)
            {
                live = coordinator.Recover(live, (kind, lifetime) =>
                {
                    _ = lifetime.Own(new Resource($"{cycle} window", events));
                    return lifetime.Own(new Resource($"{cycle} device", events));
                }, _ => { });
            }

            live.Dispose();
            Assert.Equal(Cycles, coordinator.Recoveries);
            Assert.Equal(2 * (Cycles + 1), events.Count);
            Assert.Equal(events.Count, new HashSet<string>(events).Count);
        }

        /// <summary>Builds a live selection whose resources record their own release.</summary>
        private static GraphicsSelection<Resource> Select(
            string platform, GraphicsBackendKind? forced, List<string> events)
        {
            return BackendSelector.Select(platform, forced, (kind, lifetime) =>
            {
                _ = lifetime.Own(new Resource($"{kind} window", events));
                return lifetime.Own(new Resource($"{kind} device", events));
            }, _ => { });
        }

        private sealed class Resource(string name, List<string> events, Exception failure = null) : IDisposable
        {
            public string Name { get; } = name;

            public void Dispose()
            {
                events.Add("dispose " + Name);
                if (failure != null)
                {
                    throw failure;
                }
            }
        }
    }
}
