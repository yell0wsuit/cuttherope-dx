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
    /// are traces: they are a running commentary useful only when a cutscene misbehaves.
    /// </remarks>
    internal static partial class VideoPlayerLog
    {
        /// <summary>Records a play request before anything is opened.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="moviePath">Movie the caller asked for.</param>
        /// <param name="mute">Whether playback was asked to be silent.</param>
        [LoggerMessage(Level = LogLevel.Trace, Message = "Play requested: {MoviePath}, mute={Mute}")]
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
        [LoggerMessage(Level = LogLevel.Trace, Message = "First frame: {Width}x{Height}")]
        public static partial void FirstFrame(ILogger logger, int width, int height);

        /// <summary>Records a stop request.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Trace, Message = "Stop")]
        public static partial void Stop(ILogger logger);

        /// <summary>Records a pause request.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Trace, Message = "Pause")]
        public static partial void Pause(ILogger logger);

        /// <summary>Records a resume request.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Trace, Message = "Resume")]
        public static partial void Resume(ILogger logger);

        /// <summary>Records a start request.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Trace, Message = "Start")]
        public static partial void Start(ILogger logger);

        /// <summary>Records the update that tears the finished playback down.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="hasTexture">Whether a texture is still held.</param>
        [LoggerMessage(Level = LogLevel.Trace, Message = "Update: triggering cleanup, videoTexture={HasTexture}")]
        public static partial void UpdateCleanup(ILogger logger, bool hasTexture);

        /// <summary>Records the update that notifies the game playback is over.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Trace, Message = "Update: invoking PlaybackFinished")]
        public static partial void UpdateFinishing(ILogger logger);

        /// <summary>Records the player being disposed.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Trace, Message = "Dispose")]
        public static partial void Disposing(ILogger logger);

        /// <summary>Records playback reaching its end.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="hasTexture">Whether a texture is still held.</param>
        [LoggerMessage(Level = LogLevel.Trace, Message = "Playback finished, videoTexture={HasTexture}")]
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
    }
}
