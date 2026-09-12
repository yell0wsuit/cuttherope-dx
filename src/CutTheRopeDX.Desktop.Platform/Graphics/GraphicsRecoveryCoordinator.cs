using System;
using System.Collections.Generic;
using System.Linq;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>Raised when the graphics device stops being usable while the game is running.</summary>
    public sealed class GraphicsDeviceLostException : Exception
    {
        /// <summary>Reports a lost device.</summary>
        /// <param name="message">What was being attempted when the loss showed up.</param>
        public GraphicsDeviceLostException(string message) : base(message)
        {
        }

        /// <summary>Reports a lost device behind another failure.</summary>
        /// <param name="message">What was being attempted when the loss showed up.</param>
        /// <param name="inner">The failure that revealed it.</param>
        public GraphicsDeviceLostException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    /// <summary>Raised when no renderer could replace a lost device.</summary>
    /// <param name="attempted">The renderers that were tried, in order.</param>
    /// <param name="inner">The failures they reported.</param>
    public sealed class GraphicsRecoveryFailedException(
        IReadOnlyList<GraphicsBackendKind> attempted, Exception inner)
        : Exception($"No renderer replaced the lost device (tried {string.Join(", ", attempted)}).", inner)
    {
        /// <summary>The renderers that were tried, in order.</summary>
        public IReadOnlyList<GraphicsBackendKind> Attempted { get; } = attempted;
    }

    /// <summary>
    /// Replaces a graphics device that has been lost, retrying the one that failed before moving
    /// on to the alternatives.
    /// </summary>
    /// <remarks>
    /// The renderer that was running is tried first because most losses are not its fault: a
    /// driver reset, a GPU switch or a display change takes out whatever was running, and the same
    /// backend almost always comes straight back. Only a backend that fails to come back at all is
    /// treated as the problem, and the rest of the platform order is tried after it.
    /// </remarks>
    /// <param name="platform">Platform moniker, as passed to <see cref="BackendSelector"/>.</param>
    /// <param name="forced">
    /// An explicit renderer override, if the user gave one. Recovery never switches away from it:
    /// a run that was told which renderer to use is more useful reporting that renderer's failure
    /// than quietly finishing on a different one.
    /// </param>
    public sealed class GraphicsRecoveryCoordinator(string platform, GraphicsBackendKind? forced)
    {
        /// <summary>How many devices this coordinator has replaced.</summary>
        public int Recoveries { get; private set; }

        /// <summary>The renderers to try after <paramref name="lost"/> has gone, in order.</summary>
        /// <param name="lost">The renderer that was running.</param>
        public GraphicsBackendKind[] OrderAfter(GraphicsBackendKind lost)
        {
            GraphicsBackendKind[] preference = BackendSelector.PreferenceOrder(platform, forced);
            return forced.HasValue
                ? preference
                : [lost, .. preference.Where(candidate => candidate != lost)];
        }

        /// <summary>Releases a lost device and brings up a replacement.</summary>
        /// <param name="lost">The selection that failed. It is disposed here, however this ends.</param>
        /// <param name="create">Builds one candidate, registering what it acquires as it goes.</param>
        /// <param name="validate">Draws and presents a frame, throwing if the candidate cannot.</param>
        /// <returns>The replacement selection.</returns>
        /// <exception cref="GraphicsRecoveryFailedException">No candidate came up.</exception>
        /// <remarks>
        /// The failed device is released before anything is built, because its window, context and
        /// driver handles are exactly what a replacement needs back. A teardown that throws does
        /// not stop the replacement: the device is already gone, and reporting the cleanup failure
        /// instead of recovering would turn a survivable loss into an exit.
        /// </remarks>
        public GraphicsSelection<T> Recover<T>(GraphicsSelection<T> lost,
            Func<GraphicsBackendKind, CandidateLifetime, T> create, Action<T> validate)
            where T : IDisposable
        {
            ArgumentNullException.ThrowIfNull(lost);
            GraphicsBackendKind[] order = OrderAfter(lost.Kind);
            Exception teardownFailure = null;
            try
            {
                lost.Dispose();
            }
            catch (Exception failure)
            {
                teardownFailure = failure;
            }

            try
            {
                GraphicsSelection<T> replacement = BackendSelector.Attempt(order, create, validate);
                Recoveries++;
                return replacement;
            }
            catch (AggregateException failures)
            {
                throw new GraphicsRecoveryFailedException(
                    order,
                    teardownFailure == null
                        ? failures
                        : new AggregateException([.. failures.InnerExceptions, teardownFailure]));
            }
        }
    }
}
