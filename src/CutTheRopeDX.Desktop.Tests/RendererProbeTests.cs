using System;
using System.Diagnostics;

using CutTheRopeDX.Desktop.Platform.Graphics;

using Xunit;

namespace CutTheRopeDX.Desktop.Tests
{
    public sealed class RendererProbeTests
    {
        public static bool IsWindows => OperatingSystem.IsWindows();

        [Fact(Skip = "Uses a Unix child process to simulate a native driver crash.", SkipWhen = nameof(IsWindows))]
        public void NativeCrashIsRejectedWithoutTakingDownTheCaller()
        {
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => RendererProbe.Run(Shell("ulimit -c 0; kill -SEGV $$"), 5000));
            Assert.Contains("OpenGL", failure.Message);
            Assert.Contains("exit", failure.Message);
        }

        [Fact(Skip = "Uses a Unix child process.", SkipWhen = nameof(IsWindows))]
        public void ACompletedProbeIsAccepted()
        {
            RendererProbe.Run(Shell("exit 0"), 5000);
        }

        [Fact(Skip = "Uses a Unix child process to simulate a native driver crash.", SkipWhen = nameof(IsWindows))]
        public void FirstSelectionReachesSoftwareAfterAnOpenGlProcessCrash()
        {
            using GraphicsSelection<Resource> selection = BackendSelector.Select("linux", null,
                (kind, lifetime) =>
                {
                    if (kind == GraphicsBackendKind.Vulkan)
                    {
                        throw new InvalidOperationException("Skia rejected this Vulkan device.");
                    }
                    if (RendererProbe.Required("linux", null, kind))
                    {
                        RendererProbe.Run(Shell("ulimit -c 0; kill -SEGV $$"), 5000);
                    }
                    return lifetime.Own(new Resource());
                }, resource => resource.Validated = true);

            Assert.Equal(GraphicsBackendKind.Software, selection.Kind);
            Assert.True(selection.Device.Validated);
            Assert.Collection(selection.Failures,
                failure => Assert.Equal(GraphicsBackendKind.Vulkan, failure.Kind),
                failure => Assert.Equal(GraphicsBackendKind.OpenGL, failure.Kind));
        }

        [Fact(Skip = "Uses a Unix child process.", SkipWhen = nameof(IsWindows))]
        public void DriverOutputDoesNotBlockTheProbeAndItsErrorIsReported()
        {
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => RendererProbe.Run(Shell(
                    "i=0; while [ $i -lt 4000 ]; do echo 'driver diagnostic output repeated to fill a pipe'; i=$((i+1)); done; "
                    + "echo 'Skia rejected OpenGL' >&2; exit 7"), 5000));
            Assert.Contains("code 7", failure.Message);
            Assert.Contains("Skia rejected OpenGL", failure.Message);
        }

        [Fact(Skip = "Uses a Unix child process.", SkipWhen = nameof(IsWindows))]
        public void AHungProbeIsStoppedAndRejected()
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => RendererProbe.Run(Shell("exec sleep 30"), 100));
            Assert.Contains("timed out", failure.Message);
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void OnlyAutomaticLinuxOpenGlNeedsTheProbe()
        {
            Assert.True(RendererProbe.Required("linux", null, GraphicsBackendKind.OpenGL));
            Assert.False(RendererProbe.Required("linux", GraphicsBackendKind.OpenGL, GraphicsBackendKind.OpenGL));
            Assert.False(RendererProbe.Required("linux", null, GraphicsBackendKind.Software));
            Assert.False(RendererProbe.Required("macos", null, GraphicsBackendKind.OpenGL));
        }

        [Fact]
        public void DotnetLaunchIncludesTheEntryDllAsOneArgument()
        {
            ProcessStartInfo start = RendererProbe.Command("/usr/bin/dotnet", "/game with spaces/CutTheRope-DX.dll");
            Assert.Equal("/usr/bin/dotnet", start.FileName);
            Assert.Equal(["/game with spaces/CutTheRope-DX.dll", "--sdl-probe-gl"], start.ArgumentList);
        }

        [Fact]
        public void ApphostLaunchDoesNotRepeatTheExecutableAsAnArgument()
        {
            ProcessStartInfo start = RendererProbe.Command("/tmp/.mount game/CutTheRope-DX", "/tmp/.mount game/CutTheRope-DX");
            Assert.Equal(["--sdl-probe-gl"], start.ArgumentList);
        }

        private static ProcessStartInfo Shell(string command)
        {
            ProcessStartInfo start = new("/bin/sh");
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(command);
            return start;
        }

        private sealed class Resource : IDisposable
        {
            public bool Validated { get; set; }

            public void Dispose()
            {
            }
        }
    }
}
