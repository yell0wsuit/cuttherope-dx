using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using CutTheRopeDX.Framework.Media;

using SDL3;

namespace CutTheRopeDX.Desktop.Platform.Audio
{
    /// <summary>
    /// SDL_mixer implementation of <see cref="IAudioBackend"/>. Sound effects are decoded once and
    /// played through as many voices as the game asks for; music streams from disk on a single
    /// reused voice.
    /// </summary>
    /// <remarks>
    /// No native callback is installed. The game polls playback state and repeats music through the
    /// mixer's own loop counter, so nothing here has to keep a delegate alive for the audio thread
    /// or hand work back to the main one.
    /// </remarks>
    internal sealed class SdlAudioBackend : IAudioBackend, IDisposable
    {
        /// <summary>Extension sound effects ship in.</summary>
        private const string SoundExtension = ".wav";

        /// <summary>Extension music ships in.</summary>
        private const string MusicExtension = ".flac";

        /// <summary>Name of the FLAC decoder compiled into SDL_mixer.</summary>
        private const string FlacDecoder = "DRFLAC";

        private static readonly Lock LibraryLock = new();
        private static int libraryUsers;

        private readonly nint mixer;
        private readonly string contentRoot;
        private readonly Dictionary<string, SdlMusicTrack> music = [];
        private nint musicVoice;
        private uint musicOptions;
        private bool disposed;

        private SdlAudioBackend(nint mixer, string contentRoot)
        {
            this.mixer = mixer;
            this.contentRoot = contentRoot;
        }

        /// <summary>
        /// Opens the default playback device, or reports that the platform has none.
        /// </summary>
        /// <param name="contentRoot">Absolute path to the deployed content directory.</param>
        /// <returns>The backend, or <see langword="null"/> when audio is unavailable.</returns>
        /// <remarks>
        /// A machine without a usable audio device still runs the game silently, so every failure
        /// here is reported rather than thrown; audio must never decide whether graphics start.
        /// </remarks>
        public static SdlAudioBackend TryOpen(string contentRoot)
        {
            if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
            {
                return null;
            }

            if (!AcquireLibrary())
            {
                SDL.QuitSubSystem(SDL.InitFlags.Audio);
                return null;
            }

            nint mixer = Mixer.CreateMixerDevice(SDL.AudioDeviceDefaultPlayback, 0);
            if (mixer == 0)
            {
                ReleaseLibrary();
                SDL.QuitSubSystem(SDL.InitFlags.Audio);
                return null;
            }

            return new SdlAudioBackend(mixer, contentRoot);
        }

        /// <summary>
        /// Creates a backend whose mixer renders into memory instead of a device, so playback can
        /// be verified on a machine with no audio hardware.
        /// </summary>
        /// <param name="contentRoot">Absolute path to the directory holding the sound files.</param>
        /// <param name="frequency">Sample rate of the rendered output.</param>
        /// <param name="channels">Channel count of the rendered output.</param>
        /// <returns>The backend, or <see langword="null"/> when the mixer could not be created.</returns>
        internal static SdlAudioBackend CreateForTesting(string contentRoot, int frequency, int channels)
        {
            if (!AcquireLibrary())
            {
                return null;
            }

            SDL.AudioSpec spec = new() { Format = SDL.AudioFormat.AudioS16LE, Channels = channels, Freq = frequency };
            nint specPointer = Marshal.AllocHGlobal(Marshal.SizeOf<SDL.AudioSpec>());
            try
            {
                Marshal.StructureToPtr(spec, specPointer, false);
                nint mixer = Mixer.CreateMixer(specPointer);
                if (mixer == 0)
                {
                    ReleaseLibrary();
                    return null;
                }

                return new SdlAudioBackend(mixer, contentRoot);
            }
            finally
            {
                Marshal.FreeHGlobal(specPointer);
            }
        }

        /// <summary>Renders the next span of mixed output into <paramref name="buffer"/>.</summary>
        /// <param name="buffer">Destination for the rendered samples.</param>
        /// <param name="bytes">Capacity of <paramref name="buffer"/> in bytes.</param>
        /// <returns>How many bytes the mixer produced.</returns>
        internal int RenderForTesting(nint buffer, int bytes)
        {
            return Mixer.Generate(mixer, buffer, bytes);
        }

        /// <summary>
        /// Brings SDL_mixer up, counting users so repeated open and teardown cycles leave the
        /// library in a consistent state.
        /// </summary>
        private static bool AcquireLibrary()
        {
            lock (LibraryLock)
            {
                if (libraryUsers > 0)
                {
                    libraryUsers++;
                    return true;
                }

                // The published SDL3_mixer library records no runtime search path, so its
                // dependency on SDL3 resolves only against an SDL3 image already loaded into this
                // process. Touching SDL first is what makes the mixer loadable at all.
                _ = SDL.GetError();
                if (!Mixer.Init())
                {
                    return false;
                }

                libraryUsers = 1;
                return true;
            }
        }

        private static void ReleaseLibrary()
        {
            lock (LibraryLock)
            {
                if (libraryUsers > 0 && --libraryUsers == 0)
                {
                    Mixer.Quit();
                }
            }
        }

        /// <inheritdoc />
        public ISoundEffect LoadSound(string contentPath)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            // Effects are short, are replayed constantly and often overlap, so they are decoded up
            // front; paying that cost once beats decoding on every hit.
            return new SdlSoundEffect(
                mixer, LoadAudio(ResolveAudioPath(contentRoot, contentPath, music: false), predecode: true));
        }

        /// <inheritdoc />
        public IMusicTrack LoadMusic(string contentPath)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (music.TryGetValue(contentPath, out SdlMusicTrack cached))
            {
                return cached;
            }

            // Songs are minutes long and are never freed, so they stream rather than decode; the
            // cache is what keeps replaying one from accumulating copies of it.
            SdlMusicTrack track = new(LoadAudio(ResolveAudioPath(contentRoot, contentPath, music: true), predecode: false));
            music.Add(contentPath, track);
            return track;
        }

        /// <summary>
        /// Resolves the file a sound or music content path is loaded from.
        /// </summary>
        /// <param name="contentRoot">Absolute path to the deployed content directory.</param>
        /// <param name="contentPath">Content-relative path without an extension.</param>
        /// <param name="music">Whether the path names music rather than a sound effect.</param>
        /// <returns>The absolute path to load, whether or not the file exists.</returns>
        /// <remarks>
        /// Music ships as FLAC, which the mixer decodes with its built-in decoder, while effects stay
        /// WAV because a predecoded FLAC effect holds float samples at twice the memory. A WAV is
        /// still accepted for music when no FLAC sits beside it, so a tree carrying one plays. When
        /// neither exists the FLAC path is returned, so the error names the file that should ship.
        /// </remarks>
        internal static string ResolveAudioPath(string contentRoot, string contentPath, bool music)
        {
            string basePath = Path.Combine(
                contentRoot,
                contentPath.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar));
            if (!music)
            {
                return basePath + SoundExtension;
            }

            string flac = basePath + MusicExtension;
            string wav = basePath + SoundExtension;
            return File.Exists(flac) || !File.Exists(wav) ? flac : wav;
        }

        private nint LoadAudio(string path, bool predecode)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Audio file not found: {path}", path);
            }

            nint stream = SDL.IOFromFile(path, "rb");
            if (stream == 0)
            {
                throw new InvalidDataException($"Could not open audio '{path}': {SDL.GetError()}");
            }

            uint props = SDL.CreateProperties();
            try
            {
                // The mixer closes the stream itself whether or not the load succeeds.
                _ = SDL.SetPointerProperty(props, Mixer.Props.AudioLoadIOStreamPointer, stream);
                _ = SDL.SetBooleanProperty(props, Mixer.Props.AudioLoadCloseIOBoolean, true);
                _ = SDL.SetBooleanProperty(props, Mixer.Props.AudioLoadPreDecodeBoolean, predecode);
                _ = SDL.SetPointerProperty(props, Mixer.Props.AudioLoadPreferredMixerPointer, mixer);

                // Left to choose, the mixer prefers libFLAC over its built-in decoder whenever it
                // can load one, and a system copy is enough. That decoder discards the first 4096
                // frames of every stream it opens, so each song would start about 93ms in, with a
                // click. The built-in decoder matches the reference decoder from the first frame.
                if (path.EndsWith(MusicExtension, StringComparison.OrdinalIgnoreCase))
                {
                    _ = SDL.SetStringProperty(props, Mixer.Props.AudioDecoderString, FlacDecoder);
                }

                nint audio = Mixer.LoadAudioWithProperties(props);
                return audio == 0
                    ? throw new InvalidDataException($"Could not load audio '{path}': {SDL.GetError()}")
                    : audio;
            }
            finally
            {
                SDL.DestroyProperties(props);
            }
        }

        /// <inheritdoc />
        public void PlayMusic(IMusicTrack track, bool repeating)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            nint audio = ((SdlMusicTrack)track).Audio;
            if (musicVoice == 0)
            {
                musicVoice = Mixer.CreateTrack(mixer);
                if (musicVoice == 0)
                {
                    throw new InvalidOperationException($"Could not create the music voice: {SDL.GetError()}");
                }

                musicOptions = SDL.CreateProperties();
            }

            // One voice carries whatever song is current, so starting another replaces this one
            // rather than layering over it.
            _ = Mixer.StopTrack(musicVoice, 0);
            if (!Mixer.SetTrackAudio(musicVoice, audio))
            {
                throw new InvalidOperationException($"Could not bind music to its voice: {SDL.GetError()}");
            }

            // Starting a track takes its loop count from these options, so repeating is requested
            // here rather than set on the track beforehand, where it would be discarded.
            _ = SDL.SetNumberProperty(musicOptions, Mixer.Props.PlayLoopsNumber, repeating ? -1 : 0);
            if (!Mixer.PlayTrack(musicVoice, musicOptions))
            {
                throw new InvalidOperationException($"Could not start music: {SDL.GetError()}");
            }
        }

        /// <inheritdoc />
        public void StopMusic()
        {
            if (musicVoice != 0)
            {
                _ = Mixer.StopTrack(musicVoice, 0);
            }
        }

        /// <inheritdoc />
        public void PauseMusic()
        {
            if (musicVoice != 0)
            {
                _ = Mixer.PauseTrack(musicVoice);
            }
        }

        /// <inheritdoc />
        public void ResumeMusic()
        {
            if (musicVoice != 0)
            {
                _ = Mixer.ResumeTrack(musicVoice);
            }
        }

        /// <inheritdoc />
        public AudioPlaybackState MusicState =>
            musicVoice == 0 ? AudioPlaybackState.Stopped
            : Mixer.TrackPaused(musicVoice) ? AudioPlaybackState.Paused
            : Mixer.TrackPlaying(musicVoice) ? AudioPlaybackState.Playing
            : AudioPlaybackState.Stopped;

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            // Voices read their audio on the mixer's own thread, so they go first, then the sources
            // they read, and only then the mixer that owns both.
            if (musicVoice != 0)
            {
                Mixer.DestroyTrack(musicVoice);
                musicVoice = 0;
                SDL.DestroyProperties(musicOptions);
                musicOptions = 0;
            }

            foreach (SdlMusicTrack track in music.Values)
            {
                track.Dispose();
            }

            music.Clear();
            Mixer.DestroyMixer(mixer);
            ReleaseLibrary();
        }
    }
}
