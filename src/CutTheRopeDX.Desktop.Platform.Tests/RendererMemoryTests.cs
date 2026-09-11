using System;
using System.IO;

using CutTheRopeDX.Desktop.Platform.Graphics;

using Xunit;

namespace CutTheRopeDX.Desktop.Platform.Tests
{
    /// <summary>
    /// What a launch that never came back tells the next one.
    /// </summary>
    public sealed class RendererMemoryTests : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(), $"ctrdx-renderer-{Guid.NewGuid():N}");

        private string StatePath => Path.Combine(root, "renderer.txt");

        public void Dispose()
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // A temporary directory left behind fails nothing.
            }
        }

        [Fact]
        public void AFirstLaunchBlamesNothingAndTriesEverything()
        {
            RendererMemory memory = new(StatePath);

            Assert.Null(memory.Blamed);
            Assert.Equal(
                [GraphicsBackendKind.Metal, GraphicsBackendKind.OpenGL],
                memory.Filter([GraphicsBackendKind.Metal, GraphicsBackendKind.OpenGL]));
        }

        [Fact]
        public void ARendererThatKilledTheLastLaunchIsNotTriedAgain()
        {
            new RendererMemory(StatePath).BeginAttempt(GraphicsBackendKind.Vulkan);

            RendererMemory next = new(StatePath);

            Assert.Equal(GraphicsBackendKind.Vulkan, next.Blamed);
            Assert.Equal(
                [GraphicsBackendKind.OpenGL],
                next.Filter([GraphicsBackendKind.Vulkan, GraphicsBackendKind.OpenGL]));
        }

        [Fact]
        public void ARendererThatDrewAFrameIsForgiven()
        {
            RendererMemory memory = new(StatePath);
            memory.BeginAttempt(GraphicsBackendKind.Vulkan);
            memory.RecordSuccess();

            RendererMemory next = new(StatePath);

            Assert.Null(next.Blamed);
            Assert.Equal(
                [GraphicsBackendKind.Vulkan, GraphicsBackendKind.OpenGL],
                next.Filter([GraphicsBackendKind.Vulkan, GraphicsBackendKind.OpenGL]));
        }

        [Fact]
        public void FallingBackAfterAFatalAttemptClearsTheBlameOnceSomethingWorks()
        {
            new RendererMemory(StatePath).BeginAttempt(GraphicsBackendKind.Vulkan);
            RendererMemory next = new(StatePath);

            next.BeginAttempt(GraphicsBackendKind.OpenGL);
            next.RecordSuccess();

            Assert.Null(new RendererMemory(StatePath).Blamed);
        }

        [Fact]
        public void AForcedRendererIsStillTriedSoItsFailureIsVisible()
        {
            new RendererMemory(StatePath).BeginAttempt(GraphicsBackendKind.Vulkan);

            RendererMemory next = new(StatePath);

            Assert.Equal([GraphicsBackendKind.Vulkan], next.Filter([GraphicsBackendKind.Vulkan]));
        }

        [Fact]
        public void AnUnreadableMarkerIsTreatedAsNoMarker()
        {
            _ = Directory.CreateDirectory(root);
            File.WriteAllText(StatePath, "not-a-renderer");

            Assert.Null(new RendererMemory(StatePath).Blamed);
        }

        [Fact]
        public void ADirectoryThatCannotBeWrittenDoesNotStopTheGame()
        {
            RendererMemory memory = new(Path.Combine(root, "\0bad", "renderer.txt"));

            memory.BeginAttempt(GraphicsBackendKind.Metal);
            memory.RecordSuccess();
        }

        [Fact]
        public void EveryCandidateFailingLeavesTheLastOneBlamedForTheNextLaunch()
        {
            RendererMemory memory = new(StatePath);
            GraphicsBackendKind[] order = BackendSelector.PreferenceOrder("windows", null);

            _ = Assert.Throws<AggregateException>(() => BackendSelector.Attempt<Blank>(
                memory.Filter(order),
                (kind, _) =>
                {
                    memory.BeginAttempt(kind);
                    throw new InvalidOperationException(kind.ToString());
                },
                _ => { }));

            // Nothing drew a frame, so the marker was never torn up and still names the last attempt.
            RendererMemory next = new(StatePath);
            Assert.Equal(GraphicsBackendKind.OpenGL, next.Blamed);
            Assert.Equal(
                [GraphicsBackendKind.Vulkan, GraphicsBackendKind.Angle],
                next.Filter(order));
        }

        [Fact]
        public void TheBlameRotatesSoAWorkingRendererIsReachedAgain()
        {
            RendererMemory first = new(StatePath);
            first.BeginAttempt(GraphicsBackendKind.OpenGL);
            GraphicsBackendKind[] order = BackendSelector.PreferenceOrder("windows", null);

            // The launch after a total failure skips OpenGL and blames whatever it tried last instead,
            // which is what lets the launch after that reach OpenGL again.
            RendererMemory second = new(StatePath);
            Assert.Equal([GraphicsBackendKind.Vulkan, GraphicsBackendKind.Angle], second.Filter(order));
            second.BeginAttempt(GraphicsBackendKind.Angle);

            RendererMemory third = new(StatePath);
            Assert.Equal(GraphicsBackendKind.Angle, third.Blamed);
            Assert.Contains(GraphicsBackendKind.OpenGL, third.Filter(order));
        }

        private sealed class Blank : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
