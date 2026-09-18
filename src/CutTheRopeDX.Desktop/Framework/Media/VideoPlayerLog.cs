using System;

using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.Framework.Media
{
    /// <summary>
    /// Log messages for video playback.
    /// </summary>
    /// <remarks>
    /// Both players share these, and the caller passes the logger, so which backend produced a
    /// line is carried by its category rather than by a prefix in the text. The lifecycle lines
    /// are debug: one each per cutscene, and a running commentary useful only when a cutscene
    /// misbehaves, so <c>--log-level debug</c> is what a report of a stuck cutscene asks for.
    /// Anything that can repeat every frame stays at trace.
    /// </remarks>
    internal static partial class VideoPlayerLog
    {
        /// <summary>Records a play request before anything is opened.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="moviePath">Movie the caller asked for.</param>
        /// <param name="mute">Whether playback was asked to be silent.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Play requested: {MoviePath}, mute={Mute}")]
        public static partial void PlayRequested(ILogger logger, string moviePath, bool mute);

        /// <summary>Reports a movie file that is not where it was expected.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="fullPath">Absolute path that was looked for.</param>
        [LoggerMessage(Level = LogLevel.Warning, Message = "Missing video: {FullPath}")]
        public static partial void MissingVideo(ILogger logger, string fullPath);

        /// <summary>Records which piece was absent when a texture request produced nothing.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="hasPlayer">Whether the player exists.</param>
        /// <param name="hasVideoOutput">Whether the video output exists.</param>
        /// <param name="playbackFinished">Whether playback has already ended.</param>
        /// <param name="hasTexture">Whether a texture has been produced.</param>
        [LoggerMessage(
            Level = LogLevel.Trace,
            Message = "GetTexture early return: player={HasPlayer}, videoOutput={HasVideoOutput}, "
                + "playbackFinished={PlaybackFinished}, videoTexture={HasTexture}")]
        public static partial void GetTextureEarlyReturn(
            ILogger logger, bool hasPlayer, bool hasVideoOutput, bool playbackFinished, bool hasTexture);

        /// <summary>Reports the dimensions the first decoded frame arrived at.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "First frame: {Width}x{Height}")]
        public static partial void FirstFrame(ILogger logger, int width, int height);

        /// <summary>Records a stop request.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Stop")]
        public static partial void Stop(ILogger logger);

        /// <summary>Records a pause request.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Pause")]
        public static partial void Pause(ILogger logger);

        /// <summary>Records a resume request.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Resume")]
        public static partial void Resume(ILogger logger);

        /// <summary>Records a start request.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Start")]
        public static partial void Start(ILogger logger);

        /// <summary>Records the update that tears the finished playback down.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="hasTexture">Whether a texture is still held.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Update: triggering cleanup, videoTexture={HasTexture}")]
        public static partial void UpdateCleanup(ILogger logger, bool hasTexture);

        /// <summary>Records the update that notifies the game playback is over.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Update: invoking PlaybackFinished")]
        public static partial void UpdateFinishing(ILogger logger);

        /// <summary>Records the player being disposed.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Dispose")]
        public static partial void Disposing(ILogger logger);

        /// <summary>Records playback reaching its end.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="hasTexture">Whether a texture is still held.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Playback finished, videoTexture={HasTexture}")]
        public static partial void PlaybackFinished(ILogger logger, bool hasTexture);

        /// <summary>Reports that the decoder libraries could not be brought up.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="exception">What went wrong.</param>
        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize FFmpeg")]
        public static partial void FfmpegInitializationFailed(ILogger logger, Exception exception);

        /// <summary>
        /// Reports a cutscene that was passed over, naming which half was missing.
        /// </summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="moviePath">Movie that was skipped.</param>
        /// <param name="fileExists">Whether the movie file was found.</param>
        /// <param name="librariesLoaded">Whether the decoder libraries were available.</param>
        /// <remarks>
        /// Both causes skip the cutscene silently otherwise, which makes a missing video file look
        /// exactly like a missing decoder.
        /// </remarks>
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Skipping {MoviePath}: file={FileExists}, libraries={LibrariesLoaded}")]
        public static partial void SkippingMovie(
            ILogger logger, string moviePath, bool fileExists, bool librariesLoaded);

        /// <summary>Reports a decode thread that ended on an exception.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="exception">What went wrong.</param>
        [LoggerMessage(Level = LogLevel.Error, Message = "Decode thread exception")]
        public static partial void DecodeThreadFailed(ILogger logger, Exception exception);

        /// <summary>Reports a video stream that could not be set up.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="exception">What went wrong.</param>
        [LoggerMessage(Level = LogLevel.Error, Message = "Video initialization failed")]
        public static partial void VideoInitializationFailed(ILogger logger, Exception exception);

        /// <summary>Reports playback continuing after its audio stream failed to open.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Warning, Message = "Audio init failed; continuing without audio.")]
        public static partial void AudioInitializationFailed(ILogger logger);

        /// <summary>
        /// Records a decode thread that did not stop when it was asked to.
        /// </summary>
        /// <remarks>
        /// Everything released after the join belongs to that thread while it is still running,
        /// so this is the one warning that says a teardown went ahead over resources something
        /// else may still be reading. It has never been seen on a bundled cutscene, which reads
        /// from a local file, and this is here so that stays a claim with evidence behind it.
        /// </remarks>
        /// <param name="logger">Logger to write to.</param>
        /// <param name="waitedMs">How long the thread was given to return.</param>
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Decode thread did not stop within {WaitedMs} ms; releasing anyway")]
        public static partial void DecodeThreadDidNotStop(ILogger logger, int waitedMs);

        /// <summary>Reports the stream a cutscene opened with.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="moviePath">Movie that was opened.</param>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        /// <param name="durationSeconds">Container duration, or a negative value when unknown.</param>
        /// <param name="hasAudio">Whether a soundtrack will be played.</param>
        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Opened {MoviePath}: {Width}x{Height}, {DurationSeconds:F2}s, audio={HasAudio}")]
        public static partial void Opened(
            ILogger logger, string moviePath, int width, int height, double durationSeconds, bool hasAudio);

        /// <summary>Records a pause request that had nothing to hold.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="reason">Why the request was ignored.</param>
        /// <remarks>
        /// The host pauses the movie whenever the window loses focus, whether or not one is
        /// playing, so this is routine; it is here so a log shows the request arrived.
        /// </remarks>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Pause ignored: {Reason}")]
        public static partial void PauseIgnored(ILogger logger, string reason);

        /// <summary>Records the decoder running out of packets.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="framesDecoded">Video frames decoded over the whole playback.</param>
        /// <param name="clockSeconds">Playback clock when the end was reached.</param>
        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Decode reached the end after {FramesDecoded} frames at {ClockSeconds:F2}s")]
        public static partial void DecodeReachedEnd(ILogger logger, int framesDecoded, double clockSeconds);

        /// <summary>Reports decoding ending early on an FFmpeg error.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="stage">Which call failed.</param>
        /// <param name="errorCode">The FFmpeg error code it returned.</param>
        /// <param name="framesDecoded">Video frames decoded before the failure.</param>
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Decode ended early: {Stage} returned {ErrorCode} after {FramesDecoded} frames")]
        public static partial void DecodeFailed(ILogger logger, string stage, int errorCode, int framesDecoded);

        /// <summary>Records what the audio device runs at, beside what the soundtrack is.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="sourceFrequency">Sample rate the movie's audio was encoded at.</param>
        /// <param name="sourceChannels">Channel count the movie's audio was encoded with.</param>
        /// <param name="deviceFrequency">Sample rate the device runs at, or zero if it would not say.</param>
        /// <param name="deviceChannels">Channel count the device runs at, or zero if it would not say.</param>
        /// <param name="deviceBufferMs">How much audio the device holds.</param>
        /// <param name="resampling">Whether the two rates differ, so a resampler sits between them.</param>
        /// <remarks>
        /// The two rates are what decide whether a resampler sits in the path, and a resampler is
        /// what makes the end of a soundtrack something that has to be announced rather than
        /// simply reached. A machine whose cutscenes end differently from another's differs here
        /// first, and nothing else in a log says so.
        /// </remarks>
        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Audio device {DeviceFrequency} Hz {DeviceChannels}ch buffering "
                + "{DeviceBufferMs:F0} ms; soundtrack {SourceFrequency} Hz {SourceChannels}ch, "
                + "resampling={Resampling}")]
        public static partial void AudioDeviceOpened(
            ILogger logger,
            int sourceFrequency,
            int sourceChannels,
            int deviceFrequency,
            int deviceChannels,
            double deviceBufferMs,
            bool resampling);

        /// <summary>Reports a machine with no audio output, where the movie plays silently.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "No audio device; the movie plays silently")]
        public static partial void AudioDeviceUnavailable(ILogger logger);

        /// <summary>
        /// Reports a cutscene whose decoding is over but which has not told the game so.
        /// </summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="waitedMs">How long since decoding ended.</param>
        /// <param name="paused">Whether the player is held paused.</param>
        /// <param name="pendingAudioBuffers">Decoded audio buffers not yet handed to the device.</param>
        /// <param name="deviceQueuedFrames">Sample frames the device stream has not taken yet.</param>
        /// <remarks>
        /// Completion waits on the soundtrack playing out, which takes a fraction of a second.
        /// Anything longer leaves the last frame of the movie on screen with nothing to end it,
        /// and this names which of the two things it waits on is holding it.
        /// <para>
        /// The queue is reported in frames rather than in the time they last, because what holds a
        /// stream short of empty is a handful of frames: rounded to milliseconds they read as
        /// nothing at all, which is indistinguishable from the queue this is meant to rule out.
        /// </para>
        /// </remarks>
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Decode ended {WaitedMs} ms ago but playback has not completed: paused={Paused}, "
                + "pendingAudioBuffers={PendingAudioBuffers}, deviceQueuedFrames={DeviceQueuedFrames}")]
        public static partial void CompletionStalled(
            ILogger logger, long waitedMs, bool paused, int pendingAudioBuffers, int deviceQueuedFrames);
    }
}
