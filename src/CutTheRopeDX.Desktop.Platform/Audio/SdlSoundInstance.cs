using System;

using CutTheRopeDX.Framework.Media;

using SDL3;

namespace CutTheRopeDX.Desktop.Platform.Audio
{
    /// <summary>
    /// One playing voice, backed by a mixer track bound to its effect's decoded audio.
    /// </summary>
    /// <remarks>
    /// The track is owned jointly with <see cref="SdlSoundEffect"/>: whichever is released first
    /// destroys it and tells the other, because a track outliving the audio it plays would read
    /// freed memory on the mixer's thread. Once detached the handle stays usable and inert, since
    /// <see cref="SoundMgr"/> stops and disposes instances after freeing the effect that owns them.
    /// </remarks>
    internal sealed class SdlSoundInstance : ISoundInstance
    {
        private readonly SdlSoundEffect owner;
        private readonly uint playOptions;
        private nint track;

        internal SdlSoundInstance(SdlSoundEffect owner, nint mixer, nint audio)
        {
            this.owner = owner;

            // MIX_PlayTrack takes the loop count from these options and overwrites whatever the
            // track was carrying, so the request has to live here rather than be applied ahead of
            // time -- a loop set before playing is silently discarded.
            playOptions = SDL.CreateProperties();
            track = Mixer.CreateTrack(mixer);
            if (track == 0)
            {
                SDL.DestroyProperties(playOptions);
                throw new InvalidOperationException($"Could not create an audio track: {SDL.GetError()}");
            }

            if (!Mixer.SetTrackAudio(track, audio))
            {
                Mixer.DestroyTrack(track);
                track = 0;
                SDL.DestroyProperties(playOptions);
                throw new InvalidOperationException($"Could not bind audio to a track: {SDL.GetError()}");
            }
        }

        /// <inheritdoc />
        public void Play()
        {
            if (track != 0)
            {
                _ = Mixer.PlayTrack(track, playOptions);
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (track != 0)
            {
                _ = Mixer.StopTrack(track, 0);
            }
        }

        /// <inheritdoc />
        public void Pause()
        {
            if (track != 0)
            {
                _ = Mixer.PauseTrack(track);
            }
        }

        /// <inheritdoc />
        public void Resume()
        {
            if (track != 0)
            {
                _ = Mixer.ResumeTrack(track);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Held in the play options, which are what every <see cref="Play"/> applies, so the value
        /// survives being set before playback starts and again across a stop and replay.
        /// </remarks>
        public bool IsLooped
        {
            get => SDL.GetNumberProperty(playOptions, Mixer.Props.PlayLoopsNumber, 0) != 0;
            set
            {
                // A negative count repeats forever, which is what every looped effect wants;
                // zero plays the source exactly once.
                long loops = value ? -1 : 0;
                _ = SDL.SetNumberProperty(playOptions, Mixer.Props.PlayLoopsNumber, loops);
                if (track != 0)
                {
                    // Applied live too, so changing it mid-playback takes effect immediately.
                    _ = Mixer.SetTrackLoops(track, (int)loops);
                }
            }
        }

        /// <inheritdoc />
        /// <remarks>The mixer holds the gain, so it is read back rather than mirrored here.</remarks>
        public float Volume
        {
            get => track == 0 ? 0f : Mixer.GetTrackGain(track);
            set
            {
                if (track != 0)
                {
                    _ = Mixer.SetTrackGain(track, value);
                }
            }
        }

        /// <inheritdoc />
        public AudioPlaybackState State
        {
            get
            {
                if (track == 0)
                {
                    return AudioPlaybackState.Stopped;
                }

                // A paused track reports itself as not playing, so paused has to be asked first.
                return Mixer.TrackPaused(track) ? AudioPlaybackState.Paused
                    : Mixer.TrackPlaying(track) ? AudioPlaybackState.Playing
                    : AudioPlaybackState.Stopped;
            }
        }

        /// <summary>
        /// Destroys the track without touching the effect's list, for use while that list is
        /// being drained.
        /// </summary>
        internal void Detach()
        {
            if (track != 0)
            {
                Mixer.DestroyTrack(track);
                track = 0;
                SDL.DestroyProperties(playOptions);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (track == 0)
            {
                return;
            }

            Detach();
            owner.Forget(this);
        }
    }
}
