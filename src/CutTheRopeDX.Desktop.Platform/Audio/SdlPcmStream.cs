using System;
using System.Diagnostics;

using SDL3;

namespace CutTheRopeDX.Desktop.Platform.Audio
{
    /// <summary>
    /// The PCM sink a movie's decoded soundtrack is pushed into: interleaved signed 16-bit samples
    /// in, audio device out.
    /// </summary>
    /// <remarks>
    /// The decoder produces audio faster than it is heard, so the queue depth is what paces it.
    /// That depth is reported in frames and in time rather than in buffers, because the size of a
    /// decoded packet varies with the source and says nothing about how long it lasts.
    /// <para>
    /// An empty queue means everything submitted has been handed to the device. It does not mean
    /// the last sample has been heard, so a caller that must not cut a soundtrack short waits for
    /// the device's own buffer to play out as well.
    /// </para>
    /// </remarks>
    internal sealed class SdlPcmStream : IDisposable
    {
        private const int BytesPerSample = 2;

        private readonly int frequency;
        private nint stream;
        private long emptiedAt = -1;

        private SdlPcmStream(nint stream, int frequency, int channels, bool boundToDevice,
            TimeSpan deviceBuffer)
        {
            this.stream = stream;
            this.frequency = frequency;
            BoundToDevice = boundToDevice;
            BytesPerFrame = channels * BytesPerSample;
            DeviceBuffer = deviceBuffer;
        }

        /// <summary>
        /// How long the device can still be holding audio after the queue has emptied.
        /// </summary>
        /// <remarks>
        /// The device takes whole buffers and plays them at its leisure, so the moment the queue
        /// empties is one buffer before the last sample is heard. Read from the device rather
        /// than guessed, and zero for a stream with no device behind it.
        /// </remarks>
        public TimeSpan DeviceBuffer { get; }

        /// <summary>
        /// Opens an output stream on the default playback device, or reports that there is none.
        /// </summary>
        /// <param name="frequency">Sample rate of the audio that will be submitted.</param>
        /// <param name="channels">Channel count of the audio that will be submitted.</param>
        /// <returns>The stream, or <see langword="null"/> when audio output is unavailable.</returns>
        /// <remarks>A movie still plays silently on a machine with no audio device.</remarks>
        public static SdlPcmStream TryOpen(int frequency, int channels)
        {
            if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
            {
                return null;
            }

            SDL.AudioSpec spec = new() { Format = SDL.AudioFormat.AudioS16LE, Channels = channels, Freq = frequency };
            nint stream = SDL.OpenAudioDeviceStream(SDL.AudioDeviceDefaultPlayback, in spec, null, 0);
            if (stream == 0)
            {
                SDL.QuitSubSystem(SDL.InitFlags.Audio);
                return null;
            }

            return new SdlPcmStream(stream, frequency, channels, boundToDevice: true,
                DeviceBufferOf(stream, frequency));
        }

        /// <summary>Asks the device how much it buffers, in the time that audio lasts.</summary>
        /// <param name="stream">The opened device stream.</param>
        /// <param name="fallbackFrequency">Rate to use if the device does not answer.</param>
        /// <returns>The buffer duration, or zero when the device will not say.</returns>
        private static TimeSpan DeviceBufferOf(nint stream, int fallbackFrequency)
        {
            uint device = SDL.GetAudioStreamDevice(stream);
            if (device == 0
                || !SDL.GetAudioDeviceFormat(device, out SDL.AudioSpec spec, out int sampleFrames)
                || sampleFrames <= 0)
            {
                return TimeSpan.Zero;
            }

            int rate = spec.Freq > 0 ? spec.Freq : fallbackFrequency;
            return rate > 0 ? TimeSpan.FromSeconds((double)sampleFrames / rate) : TimeSpan.Zero;
        }

        /// <summary>
        /// Creates a stream with no device behind it, so queue accounting can be exercised on a
        /// machine with no audio hardware.
        /// </summary>
        /// <param name="frequency">Sample rate of the audio that will be submitted.</param>
        /// <param name="channels">Channel count of the audio that will be submitted.</param>
        /// <returns>The stream, or <see langword="null"/> when it could not be created.</returns>
        internal static SdlPcmStream CreateForTesting(int frequency, int channels)
        {
            return CreateForTesting(frequency, channels, TimeSpan.Zero);
        }

        /// <summary>
        /// Creates a device-less stream that reports a device buffer, so the settle a soundtrack
        /// waits out can be exercised without audio hardware.
        /// </summary>
        /// <param name="frequency">Sample rate of the audio that will be submitted.</param>
        /// <param name="channels">Channel count of the audio that will be submitted.</param>
        /// <param name="deviceBuffer">What to report as the device's own buffering.</param>
        /// <returns>The stream, or <see langword="null"/> when it could not be created.</returns>
        internal static SdlPcmStream CreateForTesting(int frequency, int channels, TimeSpan deviceBuffer)
        {
            SDL.AudioSpec spec = new() { Format = SDL.AudioFormat.AudioS16LE, Channels = channels, Freq = frequency };
            nint stream = SDL.CreateAudioStream(in spec, in spec);
            return stream == 0
                ? null
                : new SdlPcmStream(stream, frequency, channels, boundToDevice: false, deviceBuffer);
        }

        /// <summary>Bytes one interleaved frame occupies, which is what turns queued bytes into frames.</summary>
        private int BytesPerFrame { get; }

        /// <summary>
        /// Whether a device is pulling from this stream. A stream created for measurement has none,
        /// so the device controls are inert on it and nothing owns the audio subsystem.
        /// </summary>
        private bool BoundToDevice { get; }

        /// <summary>Frames submitted that the device has not taken yet.</summary>
        public int QueuedFrames => stream == 0 ? 0 : SDL.GetAudioStreamQueued(stream) / BytesPerFrame;

        /// <summary>How long the queued audio lasts.</summary>
        public TimeSpan Queued => TimeSpan.FromSeconds((double)QueuedFrames / frequency);

        /// <summary>Whether everything submitted has been handed to the device.</summary>
        public bool IsDrained => QueuedFrames == 0;

        /// <summary>
        /// Whether everything submitted has not only reached the device but had time to be heard.
        /// </summary>
        /// <remarks>
        /// This is the one to gate on before stopping a stream, because stopping clears whatever
        /// the device is still holding. An empty queue is one device buffer short of the end, so
        /// a caller that stops on <see cref="IsDrained"/> cuts the tail off every soundtrack it
        /// plays. The wait restarts if more audio arrives.
        /// </remarks>
        public bool IsPlayedOut
        {
            get
            {
                if (!IsDrained)
                {
                    emptiedAt = -1;
                    return false;
                }

                if (emptiedAt < 0)
                {
                    emptiedAt = Stopwatch.GetTimestamp();
                }

                return Stopwatch.GetElapsedTime(emptiedAt) >= DeviceBuffer;
            }
        }

        /// <summary>
        /// Whether the queue is shallow enough to accept more without running ahead of playback.
        /// </summary>
        /// <param name="bound">The deepest the queue should get.</param>
        public bool HasRoomFor(TimeSpan bound)
        {
            return Queued < bound;
        }

        /// <summary>Queues one decoded block of interleaved 16-bit samples.</summary>
        /// <param name="pcm">The samples; an empty span is ignored.</param>
        public void Submit(ReadOnlySpan<byte> pcm)
        {
            ObjectDisposedException.ThrowIf(stream == 0, this);
            if (!pcm.IsEmpty)
            {
                _ = SDL.PutAudioStreamData(stream, pcm, pcm.Length);
            }
        }

        /// <summary>Starts or restarts pulling the queued audio to the device.</summary>
        public void Play()
        {
            if (stream != 0 && BoundToDevice)
            {
                _ = SDL.ResumeAudioStreamDevice(stream);
            }
        }

        /// <summary>Holds playback without discarding what is queued.</summary>
        public void Pause()
        {
            if (stream != 0 && BoundToDevice)
            {
                _ = SDL.PauseAudioStreamDevice(stream);
            }
        }

        /// <summary>Continues playback from where <see cref="Pause"/> left it.</summary>
        public void Resume()
        {
            Play();
        }

        /// <summary>
        /// Silences the stream at once, discarding audio that was queued but never heard.
        /// </summary>
        public void Stop()
        {
            if (stream == 0)
            {
                return;
            }

            Pause();

            // A skipped cutscene must go quiet immediately, so what is still queued is dropped
            // rather than allowed to play on over the screen that follows.
            _ = SDL.ClearAudioStream(stream);
        }

        /// <summary>Pulls converted audio out of an unbound stream, standing in for a device.</summary>
        /// <param name="destination">Buffer that receives the audio.</param>
        /// <returns>How many bytes were taken.</returns>
        internal int DrainForTesting(Span<byte> destination)
        {
            return stream == 0 ? 0 : SDL.GetAudioStreamData(stream, destination, destination.Length);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (stream == 0)
            {
                return;
            }

            SDL.DestroyAudioStream(stream);
            stream = 0;
            if (BoundToDevice)
            {
                SDL.QuitSubSystem(SDL.InitFlags.Audio);
            }
        }
    }
}
