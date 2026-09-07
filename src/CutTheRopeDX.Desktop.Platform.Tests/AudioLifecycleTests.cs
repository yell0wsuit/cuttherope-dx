using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using CutTheRopeDX.Desktop.Platform.Audio;
using CutTheRopeDX.Framework.Media;

using SDL3;

using Xunit;

namespace CutTheRopeDX.Desktop.Platform.Tests
{
    /// <summary>
    /// Exercises the SDL_mixer backend against a real mixer that renders into memory instead of
    /// an audio device, so playback is asserted from the samples the mixer actually produces
    /// rather than from the calls the backend made. Every test runs without audio hardware.
    /// </summary>
    public sealed class AudioLifecycleTests : IDisposable
    {
        private const int Frequency = 44100;
        private const int Channels = 2;
        private const int BytesPerFrame = Channels * 2;

        private readonly string root = Directory.CreateTempSubdirectory("ctr-audio").FullName;
        private readonly SdlAudioBackend backend;
        private readonly nint buffer = Marshal.AllocHGlobal(BytesPerFrame * Frequency);

        public AudioLifecycleTests()
        {
            WriteTone("sounds/sfx/tone.wav", milliseconds: 100, amplitude: 16000);
            WriteTone("sounds/loop.wav", milliseconds: 40, amplitude: 12000);
            backend = SdlAudioBackend.CreateForTesting(root, Frequency, Channels)
                ?? throw new InvalidOperationException($"Could not create an in-memory mixer: {SDL.GetError()}");
        }

        /// <summary>
        /// Renders the next <paramref name="milliseconds"/> of mixer output and reports the loudest
        /// sample in it. Silence is zero, so this is what distinguishes playing from stopped.
        /// </summary>
        private int Render(int milliseconds = 20)
        {
            int bytes = BytesPerFrame * Frequency * milliseconds / 1000;
            int produced = backend.RenderForTesting(buffer, bytes);
            byte[] raw = new byte[bytes];
            Marshal.Copy(buffer, raw, 0, bytes);
            short[] samples = new short[produced / 2];
            Buffer.BlockCopy(raw, 0, samples, 0, produced);
            return samples.Length == 0 ? 0 : samples.Max(Math.Abs);
        }

        /// <summary>Writes a 16-bit PCM square-wave WAV so the fixtures need no tracked assets.</summary>
        private void WriteTone(string relativePath, int milliseconds, short amplitude)
        {
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path));
            int frames = Frequency * milliseconds / 1000;
            int dataBytes = frames * BytesPerFrame;
            using BinaryWriter writer = new(File.Create(path));
            writer.Write("RIFF"u8);
            writer.Write(36 + dataBytes);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)Channels);
            writer.Write(Frequency);
            writer.Write(Frequency * BytesPerFrame);
            writer.Write((short)BytesPerFrame);
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(dataBytes);
            for (int frame = 0; frame < frames; frame++)
            {
                short value = frame % 100 < 50 ? amplitude : (short)-amplitude;
                writer.Write(value);
                writer.Write(value);
            }
        }

        [Fact]
        public void SimultaneousInstancesOfOneEffectPlayIndependently()
        {
            using ISoundEffect effect = backend.LoadSound("sounds/sfx/tone");
            ISoundInstance first = effect.CreateInstance();
            ISoundInstance second = effect.CreateInstance();
            first.Play();
            second.Play();

            Assert.Equal(AudioPlaybackState.Playing, first.State);
            Assert.Equal(AudioPlaybackState.Playing, second.State);

            first.Stop();

            Assert.Equal(AudioPlaybackState.Stopped, first.State);
            Assert.Equal(AudioPlaybackState.Playing, second.State);
            Assert.True(Render() > 0);
        }

        [Fact]
        public void AnInstancePlaysAgainAfterBeingStopped()
        {
            using ISoundEffect effect = backend.LoadSound("sounds/sfx/tone");
            ISoundInstance instance = effect.CreateInstance();
            instance.Play();
            instance.Stop();
            Assert.Equal(0, Render());

            instance.Play();

            Assert.Equal(AudioPlaybackState.Playing, instance.State);
            Assert.True(Render() > 0);
        }

        [Fact]
        public void PausingAnInstanceSilencesItAndResumingRestoresIt()
        {
            using ISoundEffect effect = backend.LoadSound("sounds/sfx/tone");
            ISoundInstance instance = effect.CreateInstance();
            instance.Play();
            Assert.True(Render() > 0);

            instance.Pause();

            Assert.Equal(AudioPlaybackState.Paused, instance.State);
            Assert.Equal(0, Render());

            instance.Resume();

            Assert.Equal(AudioPlaybackState.Playing, instance.State);
            Assert.True(Render() > 0);
        }

        [Fact]
        public void VolumeScalesTheRenderedSamples()
        {
            using ISoundEffect effect = backend.LoadSound("sounds/sfx/tone");
            ISoundInstance instance = effect.CreateInstance();
            instance.Play();
            int loud = Render();

            instance.Volume = 0.25f;
            int quiet = Render();

            Assert.Equal(0.25f, instance.Volume);
            Assert.InRange(quiet, 1, loud / 2);
        }

        [Fact]
        public void ALoopedInstanceKeepsPlayingPastTheEndOfItsSource()
        {
            using ISoundEffect effect = backend.LoadSound("sounds/sfx/tone");
            ISoundInstance instance = effect.CreateInstance();
            instance.IsLooped = true;
            instance.Play();

            // The source is 100 ms long, so an unlooped instance is finished well before this.
            _ = Render(150);

            Assert.True(instance.IsLooped);
            Assert.Equal(AudioPlaybackState.Playing, instance.State);
            Assert.True(Render() > 0);
        }

        [Fact]
        public void AnUnloopedInstanceStopsOnceItsSourceIsExhausted()
        {
            using ISoundEffect effect = backend.LoadSound("sounds/sfx/tone");
            ISoundInstance instance = effect.CreateInstance();
            instance.Play();

            _ = Render(150);

            Assert.Equal(AudioPlaybackState.Stopped, instance.State);
        }

        [Fact]
        public void DisposingAnEffectStopsAndDetachesItsLiveInstances()
        {
            ISoundEffect effect = backend.LoadSound("sounds/sfx/tone");
            ISoundInstance instance = effect.CreateInstance();
            instance.Play();

            effect.Dispose();

            Assert.Equal(AudioPlaybackState.Stopped, instance.State);
            Assert.Equal(0, Render());

            // SoundMgr stops and disposes tracked instances after freeing their effect, so the
            // detached handle has to tolerate every operation rather than fault on a dead track.
            instance.Stop();
            instance.Pause();
            instance.Resume();
            instance.Play();
            instance.Volume = 0.5f;
            instance.IsLooped = true;
            instance.Dispose();
            Assert.Equal(AudioPlaybackState.Stopped, instance.State);
        }

        [Fact]
        public void RepeatedLoadAndFreeCyclesKeepPlaying()
        {
            // One gameplay session's worth of load, play and free, repeated the way entering and
            // leaving levels does.
            for (int session = 0; session < 5; session++)
            {
                using ISoundEffect effect = backend.LoadSound("sounds/sfx/tone");
                ISoundInstance instance = effect.CreateInstance();
                instance.Play();
                Assert.True(Render() > 0, $"session {session} produced silence");
                instance.Dispose();
            }
        }

        [Fact]
        public void MusicRepeatsNativelyInsteadOfThroughTheCompletionWorkaround()
        {
            IMusicTrack track = backend.LoadMusic("sounds/loop");

            backend.PlayMusic(track, repeating: true);

            Assert.False(backend.TryInstallSongCompletionCallback(track, static (_, _) => { }));
            Assert.Equal(TimeSpan.FromMilliseconds(40), track.Duration);

            // The track is 40 ms long; native repeating is what keeps it audible beyond that.
            _ = Render(150);
            Assert.Equal(AudioPlaybackState.Playing, backend.MusicState);
            Assert.True(Render() > 0);
        }

        [Fact]
        public void MusicPausesResumesAndStops()
        {
            IMusicTrack track = backend.LoadMusic("sounds/loop");
            backend.PlayMusic(track, repeating: true);
            Assert.Equal(AudioPlaybackState.Playing, backend.MusicState);

            backend.PauseMusic();
            Assert.Equal(AudioPlaybackState.Paused, backend.MusicState);
            Assert.Equal(0, Render());

            backend.ResumeMusic();
            Assert.Equal(AudioPlaybackState.Playing, backend.MusicState);
            Assert.True(Render() > 0);

            backend.StopMusic();
            Assert.Equal(AudioPlaybackState.Stopped, backend.MusicState);
            Assert.Equal(0, Render());
        }

        [Fact]
        public void StartingASecondSongReplacesTheFirstOnOneVoice()
        {
            backend.PlayMusic(backend.LoadMusic("sounds/loop"), repeating: true);
            backend.PlayMusic(backend.LoadMusic("sounds/loop"), repeating: false);

            backend.StopMusic();

            Assert.Equal(AudioPlaybackState.Stopped, backend.MusicState);
            Assert.Equal(0, Render());
        }

        [Fact]
        public void LoadingTheSameMusicTwiceReusesOneTrack()
        {
            // SoundMgr reloads music on every transition and never frees it, so a backend that
            // decoded a fresh copy each time would grow without bound.
            Assert.Same(backend.LoadMusic("sounds/loop"), backend.LoadMusic("sounds/loop"));
        }

        [Fact]
        public void LoadingAMissingSoundThrows()
        {
            _ = Assert.Throws<FileNotFoundException>(() => backend.LoadSound("sounds/sfx/absent"));
        }

        [Fact]
        public void DisposingTheBackendTwiceIsHarmless()
        {
            ISoundEffect effect = backend.LoadSound("sounds/sfx/tone");
            effect.CreateInstance().Play();

            backend.Dispose();
            backend.Dispose();
        }

        public void Dispose()
        {
            backend.Dispose();
            Marshal.FreeHGlobal(buffer);
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
        }
    }
}
