using System;
using System.Collections.Generic;

using SDL3;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>Sets SDL hints for one renderer attempt and puts back what was there before.</summary>
    /// <remarks>
    /// SDL's hints are process-global and its GL loader reads them when it opens a library, so a
    /// value left behind belongs to whichever renderer is tried next. The library hint is what
    /// makes restoring mandatory rather than tidy: it names the file SDL opens as the OpenGL
    /// driver, so a stale one hands the next candidate the wrong library entirely.
    /// <para>
    /// A hint SDL refuses fails the attempt. SDL sets hints at normal priority, and it rejects
    /// that outright when the matching environment variable is set, so a player with
    /// <c>SDL_OPENGL_LIBRARY</c> or <c>SDL_OPENGL_ES_DRIVER</c> in their environment would
    /// otherwise get a renderer that reports itself as ANGLE while running on something else.
    /// Failing here rejects the candidate and falls through to the next one, which honors what
    /// the environment asked for instead of quietly contradicting it.
    /// </para>
    /// </remarks>
    public sealed class GlHintScope : IDisposable
    {
        private readonly Func<string, string> read;

        private readonly Func<string, string, bool> write;

        private readonly Func<string, bool> clear;

        private readonly List<(string Name, string Previous)> displaced = [];

        private bool disposed;

        /// <summary>Scopes the process's real SDL hints.</summary>
        public GlHintScope() : this(SDL.GetHint, SDL.SetHint, SDL.ResetHint)
        {
        }

        /// <summary>Scopes a stand-in for SDL, so the refusal path can be exercised.</summary>
        internal GlHintScope(Func<string, string> read, Func<string, string, bool> write,
            Func<string, bool> clear)
        {
            this.read = read;
            this.write = write;
            this.clear = clear;
        }

        /// <summary>Sets one hint, remembering what it displaced.</summary>
        /// <param name="name">The hint to set.</param>
        /// <param name="value">The value for the duration of this scope.</param>
        /// <exception cref="InvalidOperationException">SDL would not take the value.</exception>
        public void Set(string name, string value)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentException.ThrowIfNullOrEmpty(name);
            string previous = read(name);
            if (!write(name, value))
            {
                throw new InvalidOperationException(
                    $"SDL would not set the hint '{name}': {SDL.GetError()}");
            }

            // Recorded only after the write took, so a refused hint is not "restored" to a value
            // it never moved away from.
            displaced.Add((name, previous));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            for (int index = displaced.Count - 1; index >= 0; index--)
            {
                (string name, string previous) = displaced[index];
                _ = previous == null ? clear(name) : write(name, previous);
            }

            displaced.Clear();
        }
    }
}
