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
    }
}
