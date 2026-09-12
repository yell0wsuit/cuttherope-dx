using System;

using CutTheRopeDX.Framework.Media;

using SDL3;

namespace CutTheRopeDX.Desktop.Platform.Audio
{
    /// <summary>
    /// One music source. Music is not predecoded, so this holds the streaming handle rather than
    /// the whole song; the backend keeps a single voice and points it at one of these at a time.
    /// </summary>
    /// <param name="audio">The music audio this track owns.</param>
    internal sealed class SdlMusicTrack(nint audio) : IMusicTrack, IDisposable
    {
        /// <summary>The decoded or streaming audio the music voice plays.</summary>
        internal nint Audio { get; private set; } = audio;

        /// <inheritdoc />
        public TimeSpan Duration
        {
            get
            {
                long frames = Audio == 0 ? 0 : Mixer.GetAudioDuration(Audio);

                // An unknown or endless source has no duration to report; the callers that read
                // this only ever schedule against a real length.
                return frames <= 0
                    ? TimeSpan.Zero
                    : TimeSpan.FromMilliseconds(Mixer.AudioFramesToMS(Audio, frames));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Audio == 0)
            {
                return;
            }

            Mixer.DestroyAudio(Audio);
            Audio = 0;
        }
    }
}
