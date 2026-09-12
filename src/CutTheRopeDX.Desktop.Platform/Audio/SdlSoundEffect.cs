using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework.Media;

using SDL3;

namespace CutTheRopeDX.Desktop.Platform.Audio
{
    /// <summary>
    /// One decoded sound effect. The mixer separates audio data from the voices that play it, so a
    /// single decode serves every simultaneous instance.
    /// </summary>
    /// <param name="mixer">The mixer the instances play on.</param>
    /// <param name="audio">The decoded audio this effect owns.</param>
    internal sealed class SdlSoundEffect(nint mixer, nint audio) : ISoundEffect
    {
        private readonly List<SdlSoundInstance> instances = [];
        private bool disposed;

        /// <inheritdoc />
        public ISoundInstance CreateInstance()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            SdlSoundInstance instance = new(this, mixer, audio);
            instances.Add(instance);
            return instance;
        }

        /// <summary>Drops a voice that destroyed its own track.</summary>
        /// <param name="instance">The voice to stop tracking.</param>
        internal void Forget(SdlSoundInstance instance)
        {
            _ = instances.Remove(instance);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            // Every voice reads this audio, so they all have to be gone before it is freed.
            foreach (SdlSoundInstance instance in instances)
            {
                instance.Detach();
            }

            instances.Clear();
            Mixer.DestroyAudio(audio);
        }
    }
}
