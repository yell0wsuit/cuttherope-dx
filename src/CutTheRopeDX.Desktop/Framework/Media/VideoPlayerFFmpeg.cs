#if FFMPEG_BACKEND
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using CutTheRopeDX.Desktop.Platform.Audio;
using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Helpers;

using FFmpeg.AutoGen;

using Microsoft.Extensions.Logging;


namespace CutTheRopeDX.Framework.Media
{
    /// <summary>
    /// Video player implementation using FFmpeg for decoding and playback.
    /// </summary>
    /// <remarks>
    /// This player uses FFmpeg libraries for video/audio decoding and converts frames to RGBA for
    /// the renderer's frame texture. Decoding runs on a background thread to keep the main game
    /// loop responsive. Audio is played through an <see cref="SdlPcmStream"/> with resampling
    /// handled by libswresample.
    /// </remarks>
    internal sealed unsafe class VideoPlayerFFmpeg : IVideoPlayer
    {
        /// <summary>Timeout in milliseconds before considering texture ready even without frames.</summary>
        private const int TextureReadyTimeoutMs = 500;

        /// <summary>Maximum number of audio buffers to queue for playback.</summary>
        /// <summary>
        /// How far ahead of the device decoded audio is allowed to run. Bounding the queue by time
        /// rather than by a count of decoded packets keeps the lead the same whatever packet size
        /// the source happens to use.
        /// </summary>
        private static readonly TimeSpan MaxQueuedAudio = TimeSpan.FromMilliseconds(200);

        /// <summary>Bytes per audio sample (16-bit audio = 2 bytes).</summary>
        private const int BytesPerSample = 2;

        /// <summary>Lock for thread-safe frame buffer access.</summary>
        private readonly Lock bufferLock = new();

        /// <summary>Lock for thread-safe audio queue access.</summary>
        private readonly Lock audioLock = new();

        /// <summary>Gate that blocks the decode thread when playback is paused.</summary>
        private readonly ManualResetEventSlim pauseGate = new(true);

        /// <summary>Queue of decoded audio buffers waiting to be submitted.</summary>
        private readonly Queue<byte[]> pendingAudioQueue = new();

        /// <summary>Stopwatch for tracking playback time and synchronization.</summary>
        private readonly Stopwatch playbackStopwatch = new();

        /// <summary>Function to check if a file exists.</summary>
        private readonly Func<string, bool> fileExists;

        /// <summary>Whether FFmpeg native libraries were found and loaded.</summary>
        private readonly bool librariesLoaded;

        /// <summary>Tracks whether this instance has been disposed.</summary>
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoPlayerFFmpeg"/> class.
        /// </summary>
        public VideoPlayerFFmpeg()
            : this(File.Exists, baseDir => FfmpegRootPathResolver.Resolve(baseDir, Directory.Exists))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoPlayerFFmpeg"/> class with custom dependencies.
        /// </summary>
        /// <param name="fileExists">Function to check file existence.</param>
        /// <param name="resolveRootPath">Function to resolve FFmpeg library path.</param>
        internal VideoPlayerFFmpeg(Func<string, bool> fileExists, Func<string, string> resolveRootPath)
        {
            this.fileExists = fileExists;

            string ffmpegRoot = resolveRootPath(AppContext.BaseDirectory);
            if (!string.IsNullOrEmpty(ffmpegRoot))
            {
                try
                {
                    ffmpeg.RootPath = ffmpegRoot;
                    DynamicallyLoadedBindings.Initialize();
                    ffmpeg.av_log_set_level(ffmpeg.AV_LOG_WARNING);
                    librariesLoaded = true;
                }
                catch (Exception ex)
                {
                    ILogger logger = Log.For(LogCategories.MediaFFmpeg);
                    VideoPlayerLog.FfmpegInitializationFailed(logger, ex);
                }
            }
        }

        /// <inheritdoc/>
        public bool IsPaused { get; private set; }

        /// <inheritdoc/>
        public event Action PlaybackFinished;

        /// <summary>Thread-safe accessor for the playback-finished flag.</summary>
        private bool HasPlaybackFinished
        {
            get => Volatile.Read(ref field);
            set => Volatile.Write(ref field, value);
        }

        /// <summary>
        /// Whether a decode thread was left running inside resources this could not release.
        /// </summary>
        private bool abandoned;

        /// <summary>Thread-safe accessor for the stop-requested flag.</summary>
        /// <remarks>
        /// Mirrored into <see cref="interrupted"/>, which is the copy FFmpeg can see. The managed
        /// flag is only read between calls, so on its own it cannot end a read already blocked.
        /// </remarks>
        private bool HasStopRequested
        {
            get => Volatile.Read(ref field);
            set
            {
                Volatile.Write(ref field, value);
                if (interrupted != null)
                {
                    Volatile.Write(ref *interrupted, value ? 1 : 0);
                }
            }
        }

        /// <summary>
        /// The stop flag FFmpeg reads, in memory it can reach from its own thread.
        /// </summary>
        /// <remarks>
        /// Unmanaged because the callback below runs with no managed context to speak of, and
        /// because FFmpeg keeps the pointer for as long as the format context lives. One int,
        /// allocated with the player and released with it.
        /// </remarks>
        private int* interrupted = (int*)NativeMemory.AllocZeroed(sizeof(int));

        /// <summary>
        /// Tells FFmpeg to give up a blocking read.
        /// </summary>
        /// <param name="opaque">The player's stop flag.</param>
        /// <returns>Non-zero once the player has been asked to stop.</returns>
        /// <remarks>
        /// FFmpeg polls this from inside the calls that wait on I/O, which is the only way to end
        /// one early. Without it a read that does not return leaves the decode thread inside the
        /// contexts teardown is about to free, and no amount of waiting on this side changes that:
        /// the thread has to be told, not waited for.
        /// </remarks>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static int Interrupt(void* opaque)
        {
            return opaque == null ? 0 : Volatile.Read(ref *(int*)opaque);
        }

        /// <inheritdoc/>
        public void Play(string moviePath, bool mute)
        {
            if (abandoned)
            {
                // A previous cutscene left its decode thread running inside resources this never
                // got to release. Starting another would build a second set beside them, so the
                // rest of the session goes without cutscenes instead.
                ILogger abandonedLogger = Log.For(LogCategories.MediaFFmpeg);
                VideoPlayerLog.SkippingMovie(abandonedLogger, moviePath, fileExists: true, librariesLoaded: false);
                PlaybackFinished?.Invoke();
                return;
            }

            Cleanup();
            HasPlaybackFinished = false;
            HasStopRequested = false;
            frameCount = 0;
            this.mute = mute;

            string relativeVideoPath = ContentPaths.GetVideoPath(moviePath);
            string fullPath = Path.Combine(ContentPaths.GetContentRootAbsolute(), relativeVideoPath);

            if (!fileExists(fullPath) || !librariesLoaded)
            {
                ILogger logger = Log.For(LogCategories.MediaFFmpeg);
                VideoPlayerLog.SkippingMovie(logger, moviePath, fileExists(fullPath), librariesLoaded);
                PlaybackFinished?.Invoke();
                return;
            }

            if (!InitializeFfmpeg(fullPath))
            {
                Cleanup();
                PlaybackFinished?.Invoke();
                return;
            }

            EnsureTexture(videoWidth, videoHeight);
            EnsureBuffer(videoWidth, videoHeight);

            waitForStart = true;
        }

        /// <inheritdoc/>
        public ITextureHandle GetTexture()
        {
            if (videoTexture == null)
            {
                return null;
            }

            if (videoBuffer != null)
            {
                lock (bufferLock)
                {
                    if (frameReady)
                    {
                        frameReady = false;
                        videoTexture.Update(videoBuffer);
                    }
                }
            }

            return videoTexture;
        }

        /// <inheritdoc/>
        public bool IsPlaying()
        {
            // Report active until cleanup runs so callers keep invoking Update(),
            // which performs final cleanup and fires PlaybackFinished.
            return formatContext != null;
        }

        /// <inheritdoc/>
        public bool IsTextureReady()
        {
            return frameCount > 0 || (playbackStopwatch.IsRunning && playbackStopwatch.ElapsedMilliseconds > TextureReadyTimeoutMs);
        }

        /// <inheritdoc/>
        public void Stop()
        {
            if (HasPlaybackFinished)
            {
                return;
            }

            HasPlaybackFinished = true;
            playbackStopwatch.Stop();
            audioInstance?.Stop();
            Cleanup();
            PlaybackFinished?.Invoke();
        }

        /// <inheritdoc/>
        public void Pause()
        {
            if (!IsPaused)
            {
                IsPaused = true;
                playbackStopwatch.Stop();
                pauseGate.Reset();
                audioInstance?.Pause();
            }
        }

        /// <inheritdoc/>
        public void Resume()
        {
            if (IsPaused)
            {
                IsPaused = false;
                playbackStopwatch.Start();
                pauseGate.Set();
                audioInstance?.Resume();
            }
        }

        /// <inheritdoc/>
        public void Start()
        {
            if (!waitForStart)
            {
                return;
            }

            waitForStart = false;
            playbackStopwatch.Restart();
            pauseGate.Set();

            if (!mute)
            {
                audioInstance?.Play();
            }

            decodeThread = new Thread(DecodeLoop) { IsBackground = true, Name = "FFmpegDecode" };
            decodeThread.Start();
        }

        /// <inheritdoc/>
        public void Update()
        {
            if (waitForStart)
            {
                return;
            }

            if (IsPaused)
            {
                return;
            }

            if (!HasPlaybackFinished || !IsAudioPlaybackDrained())
            {
                DrainAudioQueue();
            }

            if (HasPlaybackFinished && formatContext != null && IsAudioPlaybackDrained())
            {
                Cleanup();
                IsPaused = false;
                PlaybackFinished?.Invoke();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Cleanup();
            pauseGate.Dispose();

            // Only once nothing can poll it any more. An abandoned player still has a thread
            // holding this pointer, so the one int it costs is left behind with the rest.
            if (!abandoned)
            {
                NativeMemory.Free(interrupted);
                interrupted = null;
            }
        }

        /// <summary>
        /// Background decode loop that runs on the decode thread.
        /// </summary>
        private void DecodeLoop()
        {
            try
            {
                while (!HasStopRequested && !HasPlaybackFinished)
                {
                    pauseGate.Wait();
                    if (HasStopRequested)
                    {
                        break;
                    }

                    DecodeNextFrame();

                    if (!HasPlaybackFinished && !HasStopRequested)
                    {
                        Thread.Sleep(1);
                    }
                }
            }
            catch (Exception ex)
            {
                ILogger logger = Log.For(LogCategories.MediaFFmpeg);
                VideoPlayerLog.DecodeThreadFailed(logger, ex);
                HasPlaybackFinished = true;
            }
        }

        /// <summary>
        /// Initializes FFmpeg contexts and opens the video file for decoding.
        /// </summary>
        /// <param name="filePath">Full path to the video file.</param>
        /// <returns><see langword="true" /> if initialization succeeded; otherwise, <see langword="false" />.</returns>
        private bool InitializeFfmpeg(string filePath)
        {
            try
            {
                return InitializeFfmpegCore(filePath);
            }
            catch (Exception ex)
            {
                // A native FFmpeg load or decode failure here (e.g. a missing
                // bundled dependency) must not crash the game — skip the video.
                ILogger logger = Log.For(LogCategories.MediaFFmpeg);
                VideoPlayerLog.VideoInitializationFailed(logger, ex);
                return false;
            }
        }

        /// <summary>
        /// Sets up FFmpeg format/codec contexts and opens the video for decoding.
        /// Native interop here can throw (e.g. the FFmpeg libraries fail to load on
        /// first use); it is always invoked through <see cref="InitializeFfmpeg"/>,
        /// which converts any such failure into a graceful skip.
        /// </summary>
        /// <param name="filePath">Full path to the video file.</param>
        /// <returns><see langword="true" /> if initialization succeeded; otherwise, <see langword="false" />.</returns>
        private bool InitializeFfmpegCore(string filePath)
        {
            // Allocated here rather than by the open, so the interrupt is already installed when
            // the open itself starts waiting. Opening reads the file to find the streams, so it
            // is one of the calls that can block.
            AVFormatContext* openedContext = ffmpeg.avformat_alloc_context();
            if (openedContext == null)
            {
                return false;
            }

            openedContext->interrupt_callback.callback = new AVIOInterruptCB_callback_func
            {
                Pointer = (nint)(delegate* unmanaged[Cdecl]<void*, int>)&Interrupt,
            };
            openedContext->interrupt_callback.opaque = interrupted;

            // Frees and nulls the context itself when it fails, so there is nothing left to
            // release here.
            if (ffmpeg.avformat_open_input(&openedContext, filePath, null, null) != 0)
            {
                return false;
            }

            formatContext = openedContext;

            if (ffmpeg.avformat_find_stream_info(formatContext, null) != 0)
            {
                return false;
            }

            videoStreamIndex = -1;
            for (uint i = 0; i < formatContext->nb_streams; i++)
            {
                AVStream* stream = formatContext->streams[i];
                if (stream->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    videoStreamIndex = (int)i;
                    break;
                }
            }

            if (videoStreamIndex < 0)
            {
                return false;
            }

            AVStream* videoStream = formatContext->streams[videoStreamIndex];
            AVCodec* codec = ffmpeg.avcodec_find_decoder(videoStream->codecpar->codec_id);
            if (codec == null)
            {
                return false;
            }

            videoCodecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (videoCodecContext == null)
            {
                return false;
            }

            if (ffmpeg.avcodec_parameters_to_context(videoCodecContext, videoStream->codecpar) < 0)
            {
                return false;
            }

            if (ffmpeg.avcodec_open2(videoCodecContext, codec, null) < 0)
            {
                return false;
            }

            videoWidth = videoCodecContext->width;
            videoHeight = videoCodecContext->height;
            if (videoWidth <= 0 || videoHeight <= 0)
            {
                return false;
            }

            videoFrame = ffmpeg.av_frame_alloc();
            rgbaFrame = ffmpeg.av_frame_alloc();
            if (videoFrame == null || rgbaFrame == null)
            {
                return false;
            }

            int rgbaBufferSize = checked(videoWidth * videoHeight * 4);
            rgbaBuffer = (byte*)ffmpeg.av_malloc((ulong)rgbaBufferSize);
            if (rgbaBuffer == null)
            {
                return false;
            }

            rgbaFrame->format = (int)AVPixelFormat.AV_PIX_FMT_RGBA;
            rgbaFrame->width = videoWidth;
            rgbaFrame->height = videoHeight;
            rgbaFrame->data[0] = rgbaBuffer;
            rgbaFrame->linesize[0] = videoWidth * 4;

            swsContext = ffmpeg.sws_getContext(
                videoWidth,
                videoHeight,
                videoCodecContext->pix_fmt,
                videoWidth,
                videoHeight,
                AVPixelFormat.AV_PIX_FMT_RGBA,
                (int)SwsFlags.SWS_BILINEAR,
                null,
                null,
                null);

            AVRational timeBase = videoStream->time_base;
            videoTimeBase = timeBase.num / (double)timeBase.den;
            nextFramePts = 0;

            if (swsContext == null)
            {
                return false;
            }

            packet = ffmpeg.av_packet_alloc();
            if (packet == null)
            {
                return false;
            }

            if (!mute && !InitializeAudio())
            {
                CleanupAudio();
                ILogger logger = Log.For(LogCategories.MediaFFmpeg);
                VideoPlayerLog.AudioInitializationFailed(logger);
            }

            return true;
        }

        /// <summary>
        /// Decodes the next video frame and updates the frame buffer.
        /// </summary>
        /// <remarks>
        /// Runs on the background decode thread. Uses presentation timestamps for
        /// frame timing synchronization. Also processes audio packets encountered
        /// during decoding.
        /// </remarks>
        private void DecodeNextFrame()
        {
            if (formatContext == null || packet == null || videoCodecContext == null)
            {
                HasPlaybackFinished = true;
                return;
            }

            if (!playbackStopwatch.IsRunning)
            {
                return;
            }

            double elapsedSeconds = GetPlaybackClock();
            if (elapsedSeconds < nextFramePts)
            {
                return;
            }

            while (true)
            {
                int readResult = ffmpeg.av_read_frame(formatContext, packet);
                if (readResult < 0)
                {
                    HasPlaybackFinished = true;
                    return;
                }

                if (packet->stream_index == audioStreamIndex && !mute && audioCodecContext != null)
                {
                    DecodeAudioPacket(packet);
                    ffmpeg.av_packet_unref(packet);
                    continue;
                }

                if (packet->stream_index != videoStreamIndex)
                {
                    ffmpeg.av_packet_unref(packet);
                    continue;
                }

                int sendResult = ffmpeg.avcodec_send_packet(videoCodecContext, packet);
                ffmpeg.av_packet_unref(packet);
                if (sendResult < 0)
                {
                    HasPlaybackFinished = true;
                    return;
                }

                int receiveResult = ffmpeg.avcodec_receive_frame(videoCodecContext, videoFrame);
                if (receiveResult == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                {
                    continue;
                }

                if (receiveResult == ffmpeg.AVERROR_EOF)
                {
                    HasPlaybackFinished = true;
                    return;
                }

                if (receiveResult < 0)
                {
                    HasPlaybackFinished = true;
                    return;
                }

                long pts = videoFrame->best_effort_timestamp;
                if (pts != ffmpeg.AV_NOPTS_VALUE)
                {
                    nextFramePts = pts * videoTimeBase;
                }

                _ = ffmpeg.sws_scale(
                    swsContext,
                    videoFrame->data,
                    videoFrame->linesize,
                    0,
                    videoHeight,
                    rgbaFrame->data,
                    rgbaFrame->linesize);

                int srcStride = rgbaFrame->linesize[0];
                int dstStride = videoWidth * 4;
                byte* srcBase = rgbaFrame->data[0];
                if (srcBase == null)
                {
                    HasPlaybackFinished = true;
                    return;
                }

                lock (bufferLock)
                {
                    fixed (byte* dstBase = videoBuffer)
                    {
                        for (int y = 0; y < videoHeight; y++)
                        {
                            byte* srcRow = srcBase + (y * srcStride);
                            byte* dstRow = dstBase + (y * dstStride);
                            Buffer.MemoryCopy(srcRow, dstRow, dstStride, dstStride);
                        }
                    }

                    frameReady = true;
                }

                _ = Interlocked.Increment(ref frameCount);
                return;
            }
        }

        /// <summary>
        /// Initializes audio decoding and playback components.
        /// </summary>
        /// <returns><see langword="true" /> if audio initialization succeeded or no audio stream exists; otherwise, <see langword="false" />.</returns>
        private bool InitializeAudio()
        {
            audioStreamIndex = -1;
            for (uint i = 0; i < formatContext->nb_streams; i++)
            {
                AVStream* stream = formatContext->streams[i];
                if (stream->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                {
                    audioStreamIndex = (int)i;
                    break;
                }
            }

            if (audioStreamIndex < 0)
            {
                return true;
            }

            AVStream* audioStream = formatContext->streams[audioStreamIndex];
            AVCodec* audioCodec = ffmpeg.avcodec_find_decoder(audioStream->codecpar->codec_id);
            if (audioCodec == null)
            {
                return false;
            }

            audioCodecContext = ffmpeg.avcodec_alloc_context3(audioCodec);
            if (audioCodecContext == null)
            {
                return false;
            }

            if (ffmpeg.avcodec_parameters_to_context(audioCodecContext, audioStream->codecpar) < 0)
            {
                return false;
            }

            if (ffmpeg.avcodec_open2(audioCodecContext, audioCodec, null) < 0)
            {
                return false;
            }

            audioSampleRate = audioCodecContext->sample_rate;
            int inputChannels = audioCodecContext->ch_layout.nb_channels;
            if (inputChannels <= 0)
            {
                inputChannels = 2;
            }

            audioChannels = inputChannels <= 1 ? 1 : 2;

            AVChannelLayout inLayout = audioCodecContext->ch_layout;
            AVChannelLayout outLayout = default;
            ffmpeg.av_channel_layout_default(&outLayout, audioChannels);

            SwrContext* swr = null;
            int swrResult = ffmpeg.swr_alloc_set_opts2(
                &swr,
                &outLayout,
                AVSampleFormat.AV_SAMPLE_FMT_S16,
                audioSampleRate,
                &inLayout,
                audioCodecContext->sample_fmt,
                audioCodecContext->sample_rate,
                0,
                null);

            ffmpeg.av_channel_layout_uninit(&outLayout);

            if (swrResult < 0 || swr == null)
            {
                return false;
            }

            swrContext = swr;

            if (ffmpeg.swr_init(swrContext) < 0)
            {
                return false;
            }

            audioFrame = ffmpeg.av_frame_alloc();
            if (audioFrame == null)
            {
                return false;
            }

            // A machine with no audio device still plays the movie; the soundtrack is what is lost.
            audioInstance = SdlPcmStream.TryOpen(audioSampleRate, audioChannels);

            return true;
        }

        /// <summary>
        /// Decodes an audio packet and queues the samples for playback.
        /// </summary>
        /// <param name="audioPacket">The FFmpeg audio packet to decode.</param>
        private void DecodeAudioPacket(AVPacket* audioPacket)
        {
            if (audioCodecContext == null || audioFrame == null || swrContext == null || audioInstance == null)
            {
                return;
            }

            int sendResult = ffmpeg.avcodec_send_packet(audioCodecContext, audioPacket);
            if (sendResult < 0)
            {
                return;
            }

            byte** outBuffers = stackalloc byte*[1];
            while (true)
            {
                int receiveResult = ffmpeg.avcodec_receive_frame(audioCodecContext, audioFrame);
                if (receiveResult == ffmpeg.AVERROR(ffmpeg.EAGAIN) || receiveResult == ffmpeg.AVERROR_EOF)
                {
                    return;
                }

                if (receiveResult < 0)
                {
                    HasPlaybackFinished = true;
                    return;
                }

                long delay = ffmpeg.swr_get_delay(swrContext, audioCodecContext->sample_rate);
                int dstSampleCount = (int)ffmpeg.av_rescale_rnd(
                    delay + audioFrame->nb_samples,
                    audioSampleRate,
                    audioCodecContext->sample_rate,
                    AVRounding.AV_ROUND_UP);

                int requiredBufferSize = ffmpeg.av_samples_get_buffer_size(
                    null,
                    audioChannels,
                    dstSampleCount,
                    AVSampleFormat.AV_SAMPLE_FMT_S16,
                    1);

                if (requiredBufferSize <= 0)
                {
                    continue;
                }

                EnsureAudioBuffer(requiredBufferSize);

                outBuffers[0] = audioBuffer;

                int convertedSamples = ffmpeg.swr_convert(
                    swrContext,
                    outBuffers,
                    dstSampleCount,
                    audioFrame->extended_data,
                    audioFrame->nb_samples);

                if (convertedSamples <= 0)
                {
                    continue;
                }

                int convertedSize = ffmpeg.av_samples_get_buffer_size(
                    null,
                    audioChannels,
                    convertedSamples,
                    AVSampleFormat.AV_SAMPLE_FMT_S16,
                    1);

                if (convertedSize <= 0)
                {
                    continue;
                }

                EnqueueAudioBuffer(convertedSize);
            }
        }

        /// <summary>
        /// Ensures the audio buffer has sufficient capacity.
        /// </summary>
        /// <param name="requiredSize">The minimum required buffer size in bytes.</param>
        private void EnsureAudioBuffer(int requiredSize)
        {
            if (audioBuffer != null && audioBufferCapacity >= requiredSize)
            {
                return;
            }

            if (audioBuffer != null)
            {
                ffmpeg.av_free(audioBuffer);
            }

            audioBuffer = (byte*)ffmpeg.av_malloc((ulong)requiredSize);
            audioBufferCapacity = requiredSize;
        }

        /// <summary>
        /// Copies audio data to managed memory and enqueues it for playback.
        /// Called from the decode thread.
        /// </summary>
        /// <param name="size">The size of audio data in bytes.</param>
        private void EnqueueAudioBuffer(int size)
        {
            if (audioInstance == null || audioBuffer == null)
            {
                return;
            }

            byte[] managedBuffer = new byte[size];
            Marshal.Copy((IntPtr)audioBuffer, managedBuffer, 0, size);

            lock (audioLock)
            {
                pendingAudioQueue.Enqueue(managedBuffer);
            }
        }

        /// <summary>
        /// Submits queued audio buffers to the sound effect instance.
        /// Called from the main thread.
        /// </summary>
        private void DrainAudioQueue()
        {
            if (audioInstance == null)
            {
                return;
            }

            while (audioInstance.HasRoomFor(MaxQueuedAudio))
            {
                byte[] buffer;
                lock (audioLock)
                {
                    if (pendingAudioQueue.Count == 0)
                    {
                        break;
                    }

                    buffer = pendingAudioQueue.Dequeue();
                }

                audioInstance.Submit(buffer);
                audioBytesDrained += buffer.Length;
                audioBuffersSubmitted++;
            }
        }

        /// <summary>
        /// Gets the current playback position in seconds.
        /// </summary>
        /// <returns>The elapsed playback time in seconds.</returns>
        private double GetPlaybackClock()
        {
            // Use stopwatch as primary clock - it pauses correctly and resumes properly
            // Audio sync is handled by buffering; the stopwatch provides consistent timing
            return playbackStopwatch.Elapsed.TotalSeconds;
        }

        /// <summary>
        /// Checks whether all decoded audio has fully finished playing.
        /// </summary>
        /// <returns><see langword="true" /> when no queued or pending audio buffers remain.</returns>
        private bool IsAudioPlaybackDrained()
        {
            if (mute || audioInstance == null)
            {
                return true;
            }

            lock (audioLock)
            {
                return pendingAudioQueue.Count == 0 && audioInstance.IsPlayedOut;
            }
        }

        /// <summary>
        /// Ensures the video texture exists and matches the specified dimensions.
        /// </summary>
        /// <param name="width">Required texture width.</param>
        /// <param name="height">Required texture height.</param>
        private void EnsureTexture(int width, int height)
        {
            if (videoTexture != null && width == textureWidth && height == textureHeight)
            {
                return;
            }

            videoTexture?.Dispose();

            // The renderer owns the graphics device, so it makes the frame surface; this player
            // decodes on its own thread and never learns which graphics API is running.
            videoTexture = PlatformServices.Render?.CreateVideoFrameTexture(width, height);
            textureWidth = width;
            textureHeight = height;
        }

        /// <summary>
        /// Ensures the video buffer array has sufficient capacity for the frame data.
        /// </summary>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        private void EnsureBuffer(int width, int height)
        {
            int bufferSize = checked(width * height * 4);
            if (videoBuffer == null || videoBuffer.Length != bufferSize)
            {
                videoBuffer = new byte[bufferSize];
            }
        }

        /// <summary>
        /// Releases all FFmpeg and video resources.
        /// </summary>
        /// <summary>How long a decode thread is given to notice it was asked to stop.</summary>
        /// <remarks>
        /// Generous rather than tight. Setting the stop flag now interrupts the blocking calls
        /// themselves, so a thread that has not returned within this has not merely been slow to
        /// be scheduled - it is somewhere the interrupt does not reach.
        /// </remarks>
        private const int DecodeThreadStopTimeoutMs = 5000;

        private void Cleanup()
        {
            HasStopRequested = true;
            pauseGate.Set();

            // Everything below belongs to the decode thread while it is still running, so none of
            // it may be released until that thread is out. Asking is what does the work: the stop
            // flag is the one FFmpeg polls from inside its own blocking reads, so a thread waiting
            // on I/O returns from it rather than sitting there until the wait below gives up.
            bool stopped = decodeThread == null || decodeThread.Join(DecodeThreadStopTimeoutMs);
            decodeThread = null;
            if (!stopped)
            {
                // Nothing is released. A thread still inside these contexts would be reading
                // memory this was about to hand back, and the frames of one cutscene are a far
                // smaller price than that. The player is left alone rather than reset, because
                // the thread is still reading the fields a reset would clear.
                abandoned = true;
                VideoPlayerLog.DecodeThreadDidNotStop(
                    Log.For(LogCategories.MediaFFmpeg), DecodeThreadStopTimeoutMs);
                return;
            }

            if (packet != null)
            {
                AVPacket* packetToFree = packet;
                ffmpeg.av_packet_free(&packetToFree);
                packet = null;
            }

            if (swsContext != null)
            {
                ffmpeg.sws_freeContext(swsContext);
                swsContext = null;
            }

            if (videoFrame != null)
            {
                AVFrame* frameToFree = videoFrame;
                ffmpeg.av_frame_free(&frameToFree);
                videoFrame = null;
            }

            if (rgbaFrame != null)
            {
                AVFrame* frameToFree = rgbaFrame;
                ffmpeg.av_frame_free(&frameToFree);
                rgbaFrame = null;
            }

            if (videoCodecContext != null)
            {
                AVCodecContext* contextToFree = videoCodecContext;
                ffmpeg.avcodec_free_context(&contextToFree);
                videoCodecContext = null;
            }

            if (formatContext != null)
            {
                AVFormatContext* contextToClose = formatContext;
                ffmpeg.avformat_close_input(&contextToClose);
                formatContext = null;
            }

            if (rgbaBuffer != null)
            {
                ffmpeg.av_free(rgbaBuffer);
                rgbaBuffer = null;
            }

            CleanupAudio();

            videoTexture?.Dispose();
            videoTexture = null;
            videoBuffer = null;
            frameReady = false;
            waitForStart = false;
            playbackStopwatch.Reset();
            videoStreamIndex = -1;
            videoWidth = 0;
            videoHeight = 0;
            textureWidth = 0;
            textureHeight = 0;
            frameCount = 0;
            videoTimeBase = 0;
            nextFramePts = 0;
        }

        /// <summary>
        /// Releases all audio-related resources.
        /// </summary>
        private void CleanupAudio()
        {
            pendingAudioQueue.Clear();
            audioBytesDrained = 0;
            audioBuffersSubmitted = 0;

            if (audioInstance != null)
            {
                audioInstance.Stop();
                audioInstance.Dispose();
                audioInstance = null;
            }

            if (audioFrame != null)
            {
                AVFrame* frameToFree = audioFrame;
                ffmpeg.av_frame_free(&frameToFree);
                audioFrame = null;
            }

            if (swrContext != null)
            {
                SwrContext* swrToFree = swrContext;
                ffmpeg.swr_free(&swrToFree);
                swrContext = null;
            }

            if (audioCodecContext != null)
            {
                AVCodecContext* contextToFree = audioCodecContext;
                ffmpeg.avcodec_free_context(&contextToFree);
                audioCodecContext = null;
            }

            if (audioBuffer != null)
            {
                ffmpeg.av_free(audioBuffer);
                audioBuffer = null;
                audioBufferCapacity = 0;
            }

            audioStreamIndex = -1;
            audioChannels = 0;
            audioSampleRate = 0;
        }

        /// <summary>FFmpeg format/demuxer context for the video file.</summary>
        private AVFormatContext* formatContext;

        /// <summary>Video decoder context.</summary>
        private AVCodecContext* videoCodecContext;

        /// <summary>Decoded video frame in native pixel format.</summary>
        private AVFrame* videoFrame;

        /// <summary>Video frame converted to RGBA format.</summary>
        private AVFrame* rgbaFrame;

        /// <summary>Pixel format conversion context.</summary>
        private SwsContext* swsContext;

        /// <summary>Reusable packet for reading compressed data.</summary>
        private AVPacket* packet;

        /// <summary>Native buffer for RGBA frame data.</summary>
        private byte* rgbaBuffer;

        /// <summary>Background thread for decoding video/audio frames.</summary>
        private Thread decodeThread;

        /// <summary>Index of the video stream in the container.</summary>
        private int videoStreamIndex;

        /// <summary>Video width in pixels.</summary>
        private int videoWidth;

        /// <summary>Video height in pixels.</summary>
        private int videoHeight;

        /// <summary>Current texture width.</summary>
        private int textureWidth;

        /// <summary>Current texture height.</summary>
        private int textureHeight;

        /// <summary>Number of frames decoded so far.</summary>
        private int frameCount;

        /// <summary>Indicates a new frame is ready to be uploaded to the texture.</summary>
        private bool frameReady;

        /// <summary>Indicates the player is waiting for Start() to be called.</summary>
        private bool waitForStart;

        /// <summary>Indicates audio should be muted.</summary>
        private bool mute;

        /// <summary>Time base for converting video timestamps to seconds.</summary>
        private double videoTimeBase;

        /// <summary>Presentation timestamp of the next frame to display.</summary>
        private double nextFramePts;

        /// <summary>The texture each decoded frame is written into.</summary>
        private IVideoFrameTexture videoTexture;

        /// <summary>Cached texture handle wrapper reused as long as <see cref="videoTexture"/> is unchanged.</summary>

        /// <summary>Managed buffer for transferring frame data to the texture.</summary>
        private byte[] videoBuffer;

        /// <summary>Audio decoder context.</summary>
        private AVCodecContext* audioCodecContext;

        /// <summary>Decoded audio frame.</summary>
        private AVFrame* audioFrame;

        /// <summary>Audio resampling context.</summary>
        private SwrContext* swrContext;

        /// <summary>Index of the audio stream in the container.</summary>
        private int audioStreamIndex;

        /// <summary>Number of audio channels (1 for mono, 2 for stereo).</summary>
        private int audioChannels;

        /// <summary>Audio sample rate in Hz.</summary>
        private int audioSampleRate;

        /// <summary>The PCM sink the decoded soundtrack is pushed into.</summary>
        private SdlPcmStream audioInstance;

        /// <summary>Native buffer for resampled audio data.</summary>
        private byte* audioBuffer;

        /// <summary>Current capacity of the audio buffer.</summary>
        private int audioBufferCapacity;

        /// <summary>Total bytes of audio data drained to the sound instance.</summary>
        private long audioBytesDrained;

        /// <summary>Number of audio buffers submitted to the sound instance.</summary>
        private int audioBuffersSubmitted;
    }
}
#endif
