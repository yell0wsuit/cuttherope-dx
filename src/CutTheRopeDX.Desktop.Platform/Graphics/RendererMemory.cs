using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using CutTheRopeDX.Framework.Diagnostics;

using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>
    /// Remembers which renderer a previous launch died while bringing up, so the next launch does
    /// not try it again.
    /// </summary>
    /// <remarks>
    /// Selection already falls back when a renderer refuses to start, but only when it refuses in a
    /// way that can be caught. A graphics driver that faults inside its own initialization takes
    /// the process with it, and an access violation in native code is not an exception any
    /// <c>catch</c> sees. Without this, such a machine never starts the game: every launch tries
    /// that renderer, and every attempt kills it.
    /// <para>
    /// What is stored is deliberately not the answer. A marker is written immediately before a
    /// renderer is brought up and cleared once one has drawn a frame, so the only thing a later
    /// launch can conclude is that a particular attempt never came back. A renderer that works is
    /// re-tried from scratch every time, which is what lets a driver installed since last time be
    /// noticed at once.
    /// </para>
    /// </remarks>
    /// <param name="statePath">File the marker is kept in.</param>
    public sealed class RendererMemory(string statePath)
    {
        /// <summary>The renderer a previous launch did not survive, if there was one.</summary>
        public GraphicsBackendKind? Blamed { get; private set; } = Read(statePath);

        /// <summary>Drops the renderer that killed a previous launch from an attempt order.</summary>
        /// <param name="order">The renderers that would otherwise be tried, best first.</param>
        /// <returns>
        /// The order with the blamed renderer removed, or the order unchanged when that would
        /// leave nothing to try.
        /// </returns>
        /// <remarks>
        /// An order of one is left alone on purpose. That is what a forced renderer produces, and
        /// a run told which renderer to use is better off failing in the open than silently
        /// running on another one.
        /// </remarks>
        public GraphicsBackendKind[] Filter(IReadOnlyList<GraphicsBackendKind> order)
        {
            ArgumentNullException.ThrowIfNull(order);
            if (Blamed is not { } blamed || order.Count <= 1)
            {
                return [.. order];
            }

            GraphicsBackendKind[] remaining = [.. order.Where(kind => kind != blamed)];
            return remaining.Length == 0 ? [.. order] : remaining;
        }

        /// <summary>
        /// Records that <paramref name="kind"/> is being brought up now.
        /// </summary>
        /// <param name="kind">The renderer about to be created.</param>
        /// <remarks>
        /// This has to reach the disk before the attempt, because the failure it guards against
        /// leaves no other trace.
        /// </remarks>
        public void BeginAttempt(GraphicsBackendKind kind)
        {
            Write(kind.ToString());
        }

        /// <summary>Clears the marker once a renderer has proved it can run.</summary>
        public void RecordSuccess()
        {
            Blamed = null;
            Write(string.Empty);
        }

        /// <summary>Clears the marker for a candidate that failed but came back.</summary>
        /// <remarks>
        /// The marker exists to catch a renderer that never returns at all, and a caught failure is
        /// not that: the catch block only runs because the driver handed control back. Clearing it
        /// here — once the candidate's resources have finished being released, so a fault during
        /// cleanup is still covered — keeps that protection and stops a renderer being skipped on the
        /// next launch over a failure it has since recovered from.
        /// </remarks>
        public void Absolve()
        {
            Blamed = null;
            Write(string.Empty);
        }

        private void Write(string value)
        {
            try
            {
                string directory = Path.GetDirectoryName(statePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    _ = Directory.CreateDirectory(directory);
                }

                File.WriteAllText(statePath, value);
            }
            catch (Exception failure) when (IsFileSystemFailure(failure))
            {
                // A machine that cannot record this still runs the game; it only loses the
                // protection on the next launch, which is where it was before this existed.
                ILogger logger = Log.For(LogCategories.SdlGraphics);
                RendererMemoryLog.MarkerWriteFailed(logger, statePath, failure);
            }
        }

        /// <summary>
        /// Whether a failure is the filesystem refusing, rather than a fault worth reporting.
        /// </summary>
        /// <param name="failure">The failure to classify.</param>
        /// <remarks>
        /// The path is composed from a save directory this class does not choose, so a malformed
        /// one reaches here as an argument failure rather than an I/O one. Letting that escape
        /// would take the game down at startup over a note it only keeps as a courtesy.
        /// </remarks>
        private static bool IsFileSystemFailure(Exception failure)
        {
            return failure is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException;
        }

        private static GraphicsBackendKind? Read(string path)
        {
            try
            {
                return File.Exists(path)
                    && Enum.TryParse(File.ReadAllText(path).Trim(), out GraphicsBackendKind kind)
                    ? kind
                    : null;
            }
            catch (Exception failure) when (IsFileSystemFailure(failure))
            {
                ILogger logger = Log.For(LogCategories.SdlGraphics);
                RendererMemoryLog.MarkerReadFailed(logger, path, failure);
                return null;
            }
        }
    }

    /// <summary>Log messages for the renderer blame marker.</summary>
    internal static partial class RendererMemoryLog
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Could not write the renderer marker '{Path}'")]
        public static partial void MarkerWriteFailed(ILogger logger, string path, Exception exception);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read the renderer marker '{Path}'")]
        public static partial void MarkerReadFailed(ILogger logger, string path, Exception exception);
    }
}
