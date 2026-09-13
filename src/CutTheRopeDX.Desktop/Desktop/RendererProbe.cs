using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using CutTheRopeDX.Desktop.Platform.Graphics;

namespace CutTheRopeDX.Desktop
{
    /// <summary>Isolates Linux OpenGL initialization from native driver crashes.</summary>
    internal static class RendererProbe
    {
        internal const string Argument = "--sdl-probe-gl";

        internal static bool Required(string platform, GraphicsBackendKind? forced, GraphicsBackendKind kind)
        {
            return platform == "linux" && forced == null && kind == GraphicsBackendKind.OpenGL;
        }

        internal static void Check()
        {
            Run(Command(Environment.ProcessPath, Environment.GetCommandLineArgs()[0]), 15000);
        }

        internal static ProcessStartInfo Command(string processPath, string entryPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(processPath);
            ProcessStartInfo start = new(processPath);
            // Framework-dependent runs restart dotnet with the DLL; apphosts and AppImage's
            // mounted executable restart directly. ArgumentList preserves spaces in either path.
            if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                start.ArgumentList.Add(entryPath);
            }
            start.ArgumentList.Add(Argument);
            return start;
        }

        internal static void Run(ProcessStartInfo start, int timeoutMilliseconds)
        {
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.CreateNoWindow = true;
            using Process process = Process.Start(start)
                ?? throw new InvalidOperationException("Could not start the OpenGL validation process.");
            using CancellationTokenSource cancellation = new();
            Task<string> output = Drain(process.StandardOutput, cancellation.Token);
            Task<string> error = Drain(process.StandardError, cancellation.Token);
            try
            {
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    try { process.Kill(); }
                    catch (InvalidOperationException) when (process.HasExited) { }
                    _ = process.WaitForExit(2000);
                    throw new InvalidOperationException("OpenGL validation process timed out; skipping OpenGL.");
                }

                // Drain both pipes concurrently so a noisy driver cannot block on a full pipe.
                // Bound the wait too: an inherited pipe held by a descendant must not stall us.
                _ = Task.WaitAll([output, error], 1000);
                if (process.ExitCode != 0)
                {
                    string detail = error.IsCompletedSuccessfully ? error.Result.Trim() : string.Empty;
                    throw new InvalidOperationException(
                        $"OpenGL validation process exited with code {process.ExitCode}; skipping OpenGL. {detail}".TrimEnd());
                }
            }
            finally
            {
                cancellation.Cancel();
            }
        }

        private static async Task<string> Drain(StreamReader reader, CancellationToken cancellation)
        {
            const int limit = 2048;
            StringBuilder text = new();
            char[] buffer = new char[512];
            try
            {
                int count;
                while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellation).ConfigureAwait(false)) != 0)
                {
                    _ = text.Append(buffer, 0, Math.Min(count, limit - text.Length));
                }
            }
            catch (OperationCanceledException)
            {
            }
            return text.ToString();
        }
    }
}
