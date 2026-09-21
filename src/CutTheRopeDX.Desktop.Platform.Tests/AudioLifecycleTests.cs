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

        [Fact]
        public void MusicLoadsFromFlacWhenOneShips()
        {
            Touch("sounds/theme.flac");
            Touch("sounds/theme.wav");

            Assert.Equal(
                Path.Combine(root, "sounds", "theme.flac"),
                SdlAudioBackend.ResolveAudioPath(root, "sounds/theme", music: true));
        }

        [Fact]
        public void MusicFallsBackToWavWhenNoFlacShips()
        {
            Assert.Equal(
                Path.Combine(root, "sounds", "loop.wav"),
                SdlAudioBackend.ResolveAudioPath(root, "sounds/loop", music: true));
        }

        [Fact]
        public void SoundEffectsAlwaysLoadFromWav()
        {
            Touch("sounds/sfx/tone.flac");

            Assert.Equal(
                Path.Combine(root, "sounds", "sfx", "tone.wav"),
                SdlAudioBackend.ResolveAudioPath(root, "sounds/sfx/tone", music: false));
        }

        /// <summary>Creates an empty file, for tests that only care whether a path exists.</summary>
        private void Touch(string relativePath)
        {
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, []);
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
        private void WriteTone(string relativePath, int milliseconds, short amplitude, int frequency = Frequency)
        {
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path));
            int frames = frequency * milliseconds / 1000;
            int dataBytes = frames * BytesPerFrame;
            using BinaryWriter writer = new(File.Create(path));
            writer.Write("RIFF"u8);
            writer.Write(36 + dataBytes);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)Channels);
            writer.Write(frequency);
            writer.Write(frequency * BytesPerFrame);
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

        /// <summary>
        /// Writes a 16-bit FLAC of uncompressed (verbatim) frames: a square wave for
        /// <paramref name="toneMilliseconds"/>, then silence to <paramref name="milliseconds"/>.
        /// </summary>
        private void WriteFlacTone(string relativePath, int milliseconds, int toneMilliseconds, short amplitude)
        {
            const int BlockSize = 4096;
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path));
            int frames = Frequency * milliseconds / 1000;
            int toneFrames = Frequency * toneMilliseconds / 1000;

            using MemoryStream file = new();
            file.Write("fLaC"u8);

            // STREAMINFO, flagged as the last metadata block, 34 bytes long.
            file.Write([0x80, 0, 0, 34]);
            WriteBigEndian(file, BlockSize, 2);
            WriteBigEndian(file, BlockSize, 2);
            WriteBigEndian(file, 0, 3);
            WriteBigEndian(file, 0, 3);
            WriteBigEndian(file, ((ulong)Frequency << 44) | ((ulong)(Channels - 1) << 41) | (15UL << 36) | (uint)frames, 8);
            file.Write(new byte[16]);

            for (int start = 0, index = 0; start < frames; start += BlockSize, index++)
            {
                int count = Math.Min(BlockSize, frames - start);
                using MemoryStream frame = new();

                // Fixed block size; size read from the header end; 44.1 kHz; independent stereo;
                // 16-bit samples; frame number below 128, so one byte.
                frame.Write([0xFF, 0xF8, 0x79, 0x18, (byte)index]);
                WriteBigEndian(frame, count - 1, 2);
                frame.WriteByte(Crc8(frame.ToArray()));

                for (int channel = 0; channel < Channels; channel++)
                {
                    frame.WriteByte(0x02);
                    for (int i = start; i < start + count; i++)
                    {
                        short value = i >= toneFrames ? (short)0 : i % 100 < 50 ? amplitude : (short)-amplitude;
                        WriteBigEndian(frame, value & 0xFFFF, 2);
                    }
                }

                WriteBigEndian(frame, Crc16(frame.ToArray()), 2);
                frame.WriteTo(file);
            }

            File.WriteAllBytes(path, file.ToArray());
        }

        private static void WriteBigEndian(Stream stream, ulong value, int bytes)
        {
            for (int shift = (bytes - 1) * 8; shift >= 0; shift -= 8)
            {
                stream.WriteByte((byte)(value >> shift));
            }
        }

        private static void WriteBigEndian(Stream stream, int value, int bytes)
        {
            WriteBigEndian(stream, (uint)value, bytes);
        }

        private static byte Crc8(byte[] data)
        {
            int crc = 0;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x80) != 0 ? (crc << 1) ^ 0x07 : crc << 1;
                }
            }

            return (byte)crc;
        }

        private static int Crc16(byte[] data)
        {
            int crc = 0;
            foreach (byte b in data)
            {
                crc ^= b << 8;
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x8000) != 0 ? (crc << 1) ^ 0x8005 : crc << 1;
                }
            }

            return crc & 0xFFFF;
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
        public void MusicRepeatsNatively()
        {
            IMusicTrack track = backend.LoadMusic("sounds/loop");

            backend.PlayMusic(track, repeating: true);

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
        public void EffectsAtAnotherRateFillTheirFirstBuffer()
        {
            // A voice resampling on the fly comes up short on the first buffer it mixes, by the
            // frames its resampler holds back, and the mixer leaves that shortfall silent: a gap
            // cut into the sound just after it starts, heard as a pop.
            WriteTone("sounds/sfx/low.wav", milliseconds: 100, amplitude: 16000, frequency: Frequency / 2);
            using ISoundEffect effect = backend.LoadSound("sounds/sfx/low");
            effect.CreateInstance().Play();

            const int ChunkBytes = 1024 * BytesPerFrame;
            Assert.Equal(ChunkBytes, backend.RenderForTesting(buffer, ChunkBytes));
        }

        [Fact]
        public void FlacMusicPlaysFromItsFirstFrame()
        {
            // Sound for the first 40 ms, then silence. A decoder that discards the opening block
            // of the stream, as libFLAC does, would begin in the silence.
            WriteFlacTone("sounds/opening.flac", milliseconds: 200, toneMilliseconds: 40, amplitude: 12000);
            IMusicTrack track = backend.LoadMusic("sounds/opening");

            backend.PlayMusic(track, repeating: false);

            Assert.Equal(TimeSpan.FromMilliseconds(200), track.Duration);
            Assert.True(Render() > 0);
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
