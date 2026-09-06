using System;
using System.Collections.Generic;

using CutTheRopeDX.Desktop.Platform.Graphics;

using Xunit;

namespace CutTheRopeDX.Desktop.Platform.Tests
{
    public sealed class BackendSelectionTests
    {
        [Theory]
        [InlineData("windows", GraphicsBackendKind.Vulkan, GraphicsBackendKind.OpenGL)]
        [InlineData("linux", GraphicsBackendKind.Vulkan, GraphicsBackendKind.OpenGL)]
        [InlineData("macos", GraphicsBackendKind.Metal, GraphicsBackendKind.OpenGL)]
        public void RejectsFailedFramesInPlatformOrder(string platform, params GraphicsBackendKind[] expected)
        {
            List<GraphicsBackendKind> attempts = [];
            List<string> events = [];
            using GraphicsSelection<Resource> selected = BackendSelector.Select(platform, null, (kind, lifetime) =>
            {
                attempts.Add(kind);
                return lifetime.Own(new Resource(kind.ToString(), events));
            }, resource =>
            {
                events.Add("validate " + resource.Name);
                if (resource.Name != "OpenGL")
                {
                    throw new InvalidOperationException(resource.Name);
                }
            });
            Assert.Equal(expected, attempts);
            Assert.Equal("OpenGL", selected.Device.Name);
            Assert.DoesNotContain("dispose OpenGL", events);
            for (int i = 0; i < expected.Length - 1; i++)
            {
                Assert.Equal("validate " + expected[i], events[i * 2]);
                Assert.Equal("dispose " + expected[i], events[(i * 2) + 1]);
            }
        }

        [Fact]
        public void PartialInitializationCleansUpInReverseOrderBeforeFallback()
        {
            List<string> events = [];
            using GraphicsSelection<Resource> selected = BackendSelector.Select("macos", null, (kind, lifetime) =>
            {
                if (kind == GraphicsBackendKind.Metal)
                {
                    _ = lifetime.Own(new Resource("window", events));
                    _ = lifetime.Own(new Resource("device", events));
                    _ = lifetime.Own(new Resource("context", events));
                    throw new InvalidOperationException("surface creation failed");
                }
                Assert.Equal(["dispose context", "dispose device", "dispose window"], events);
                return lifetime.Own(new Resource("GL", events));
            }, _ => { });
            Assert.Equal("GL", selected.Device.Name);
        }

        [Fact]
        public void ExhaustionPreservesInitializationAndCleanupFailures()
        {
            List<string> events = [];
            InvalidOperationException original = new("first frame failed");
            InvalidOperationException cleanup = new("context cleanup failed");
            AggregateException error = Assert.Throws<AggregateException>(() =>
                BackendSelector.Select("macos", GraphicsBackendKind.Metal, (kind, lifetime) =>
                {
                    _ = lifetime.Own(new Resource("window", events));
                    return lifetime.Own(new Resource("context", events, cleanup));
                }, _ => throw original));
            Assert.Contains(original, error.Flatten().InnerExceptions);
            Assert.Contains(cleanup, error.Flatten().InnerExceptions);
            Assert.Equal(["dispose context", "dispose window"], events);
        }

        [Fact]
        public void ForcedBackendNeverAttemptsFallback()
        {
            List<GraphicsBackendKind> attempts = [];
            InvalidOperationException original = new("device failure");
            AggregateException error = Assert.Throws<AggregateException>(() =>
                BackendSelector.Select<Resource>("windows", GraphicsBackendKind.OpenGL, (kind, _) =>
                {
                    attempts.Add(kind);
                    throw original;
                }, _ => { }));
            Assert.Equal([GraphicsBackendKind.OpenGL], attempts);
            Assert.Contains(original, error.Flatten().InnerExceptions);
        }

        [Fact]
        public void WindowsFallsBackToAngleWhenEveryEarlierBackendFails()
        {
            List<GraphicsBackendKind> attempts = [];
            List<string> events = [];
            using GraphicsSelection<Resource> selected = BackendSelector.Select("windows", null, (kind, lifetime) =>
            {
                attempts.Add(kind);
                return lifetime.Own(new Resource(kind.ToString(), events));
            }, resource =>
            {
                if (resource.Name != "Angle")
                {
                    throw new InvalidOperationException(resource.Name);
                }
            });
            Assert.Equal(
                [
                    GraphicsBackendKind.Vulkan,
                    GraphicsBackendKind.OpenGL,
                    GraphicsBackendKind.Angle,
                ],
                attempts);
            Assert.Equal(GraphicsBackendKind.Angle, selected.Kind);
        }

        [Theory]
        [InlineData("linux")]
        [InlineData("macos")]
        public void AngleIsOfferedOnlyWhereItsNativeAssetsShip(string platform)
        {
            List<GraphicsBackendKind> attempts = [];
            _ = Assert.Throws<AggregateException>(() =>
                BackendSelector.Select<Resource>(platform, null, (kind, _) =>
                {
                    attempts.Add(kind);
                    throw new InvalidOperationException(kind.ToString());
                }, _ => { }));
            Assert.DoesNotContain(GraphicsBackendKind.Angle, attempts);
        }

        [Fact]
        public void ExhaustedCandidatesPreserveEveryFailure()
        {
            List<Exception> failures = [];
            AggregateException error = Assert.Throws<AggregateException>(() =>
                BackendSelector.Select<Resource>("windows", null, (kind, _) =>
                {
                    InvalidOperationException failure = new(kind.ToString());
                    failures.Add(failure);
                    throw failure;
                }, _ => { }));
            Assert.Equal(3, failures.Count);
            Assert.Equal(failures, error.Flatten().InnerExceptions);
        }

        [Fact]
        public void LifetimeDisposalIsIdempotent()
        {
            List<string> events = [];
            CandidateLifetime lifetime = new();
            _ = lifetime.Own(new Resource("device", events));
            lifetime.Dispose();
            lifetime.Dispose();
            Assert.Equal(["dispose device"], events);
        }

        [Fact]
        public void SuccessfulSelectionRetainsAllDependenciesUntilDisposed()
        {
            List<string> events = [];
            GraphicsSelection<Resource> selected = BackendSelector.Select("macos", null, (kind, lifetime) =>
            {
                _ = lifetime.Own(new Resource("window", events));
                return lifetime.Own(new Resource("device", events));
            }, _ => { });
            Assert.Empty(events);
            selected.Dispose();
            selected.Dispose();
            Assert.Equal(["dispose device", "dispose window"], events);
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
