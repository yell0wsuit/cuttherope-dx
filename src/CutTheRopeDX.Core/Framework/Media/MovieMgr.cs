using System;

using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Framework.Platform;

using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.Framework.Media
{
    /// <summary>
    /// Manages video playback and provides a unified interface for movie operations.
    /// </summary>
    /// <remarks>
    /// This class wraps platform-specific video player implementations (FFmpeg, AVFoundation,
    /// or the no-op stub) and notifies delegates when playback finishes.
    /// </remarks>
    internal sealed class MovieMgr : FrameworkTypes, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MovieMgr"/> class.
        /// </summary>
        /// <remarks>
        /// Creates a platform-specific video player (FFmpeg, AVFoundation on macOS 26+,
        /// or the no-op stub when neither is built in).
        /// </remarks>
        public MovieMgr()
        {
            // Which concrete backend (AVFoundation/FFmpeg/no-op stub) is available depends on
            // compile-time constants only the desktop host's build defines, so the desktop host
            // resolves and registers the factory at boot. Headless never sets it, so the Core-owned
            // no-op stub is used there.
            videoPlayer = PlatformServices.VideoPlayerFactory?.Invoke() ?? new VideoPlayerNone();
            videoPlayer.PlaybackFinished += OnPlaybackFinished;
        }

        /// <summary>
        /// Prepares and initiates video playback from the specified path.
        /// </summary>
        /// <param name="moviePath">The relative path to the video file without extension.</param>
        /// <param name="mute">If <see langword="true" />, audio will be muted during playback.</param>
        public void PlayURL(string moviePath, bool mute)
        {
            ILogger logger = Logger;
            string listener = delegateMovieMgrDelegate?.GetType().Name ?? "nobody";
            MovieMgrLog.PlayRequested(logger, moviePath, mute, listener);
            url = moviePath;
            videoPlayer.Play(moviePath, mute);
        }

        /// <summary>
        /// Gets the current video frame as a texture.
        /// </summary>
        /// <returns>
        /// An <see cref="ITextureHandle"/> containing the current video frame, or <see langword="null" />
        /// if no video is playing or playback has finished.
        /// </returns>
        public ITextureHandle GetTexture()
        {
            return videoPlayer.GetTexture();
        }

        /// <summary>
        /// Determines whether a video is currently loaded and potentially playing.
        /// </summary>
        /// <returns><see langword="true" /> if a video is active; otherwise, <see langword="false" />.</returns>
        public bool IsPlaying()
        {
            return videoPlayer.IsPlaying();
        }

        /// <summary>
        /// Determines whether the video texture is ready for rendering.
        /// </summary>
        /// <returns><see langword="true" /> if the texture can be rendered; otherwise, <see langword="false" />.</returns>
        public bool IsTextureReady()
        {
            return videoPlayer.IsTextureReady();
        }

        /// <summary>
        /// Stops the current video playback.
        /// </summary>
        public void Stop()
        {
            if (!videoPlayer.IsPlaying())
            {
                return;
            }
            ILogger logger = Logger;
            MovieMgrLog.StopRequested(logger, url);
            videoPlayer.Stop();
        }

        /// <summary>
        /// Pauses the current video playback.
        /// </summary>
        public void Pause()
        {
            ILogger logger = Logger;
            bool playing = videoPlayer.IsPlaying();
            MovieMgrLog.PauseRequested(logger, playing, videoPlayer.IsPaused);
            videoPlayer.Pause();
        }

        /// <summary>
        /// Determines whether playback is currently paused.
        /// </summary>
        /// <returns><see langword="true" /> if playback is paused; otherwise, <see langword="false" />.</returns>
        public bool IsPaused()
        {
            return videoPlayer.IsPaused;
        }

        /// <summary>
        /// Resumes video playback after being paused.
        /// </summary>
        public void Resume()
        {
            ILogger logger = Logger;
            bool playing = videoPlayer.IsPlaying();
            MovieMgrLog.ResumeRequested(logger, playing, videoPlayer.IsPaused);
            videoPlayer.Resume();
        }

        /// <summary>
        /// Starts video playback after a video has been prepared with <see cref="PlayURL"/>.
        /// </summary>
        public void Start()
        {
            videoPlayer.Start();
        }

        /// <summary>
        /// Updates the video player state each frame.
        /// </summary>
        public void Update()
        {
            videoPlayer.Update();
        }

        /// <summary>
        /// Handles the video player's playback finished event and notifies the delegate.
        /// </summary>
        private void OnPlaybackFinished()
        {
            ILogger logger = Logger;
            if (delegateMovieMgrDelegate == null)
            {
                MovieMgrLog.FinishedUnobserved(logger, url);
                return;
            }

            string listener = delegateMovieMgrDelegate.GetType().Name;
            MovieMgrLog.Finished(logger, url, listener);
            delegateMovieMgrDelegate.MoviePlaybackFinished(url);
        }

        /// <summary>
        /// Releases all resources used by the movie manager.
        /// </summary>
        public new void Dispose()
        {
            videoPlayer.PlaybackFinished -= OnPlaybackFinished;
            videoPlayer.Dispose();
        }

        /// <summary>The logger every line from the manager goes to.</summary>
        private static ILogger Logger => Log.For(LogCategories.MediaMovie);

#pragma warning disable CA1859
        /// <summary>The underlying video player implementation.</summary>
        private readonly IVideoPlayer videoPlayer;
#pragma warning restore CA1859

        /// <summary>The URL or path of the currently playing video.</summary>
        public string url;

        /// <summary>Delegate to notify when movie playback events occur.</summary>
        public IMovieMgrDelegate delegateMovieMgrDelegate;
    }

    /// <summary>Log messages for cutscene requests and completion.</summary>
    /// <remarks>
    /// These are the game's side of a cutscene, and the player backends log their own side
    /// under their own categories. Reading the two together shows where a cutscene that never
    /// ended got stuck: a finish the player never reported, or one nobody acted on.
    /// </remarks>
    internal static partial class MovieMgrLog
    {
        /// <summary>Records a cutscene being asked for.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="url">Movie requested.</param>
        /// <param name="mute">Whether it plays silently.</param>
        /// <param name="listener">Type of whoever will be told it finished.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Play {Url}, mute={Mute}, listener={Listener}")]
        public static partial void PlayRequested(ILogger logger, string url, bool mute, string listener);

        /// <summary>Records a skip of the cutscene on screen.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="url">Movie being skipped.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Stop {Url}")]
        public static partial void StopRequested(ILogger logger, string url);

        /// <summary>Records a pause request, which arrives on every focus loss.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="playing">Whether a cutscene is loaded.</param>
        /// <param name="paused">Whether it was already paused.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Pause requested: playing={Playing}, alreadyPaused={Paused}")]
        public static partial void PauseRequested(ILogger logger, bool playing, bool paused);

        /// <summary>Records a resume request.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="playing">Whether a cutscene is loaded.</param>
        /// <param name="paused">Whether it was paused.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Resume requested: playing={Playing}, paused={Paused}")]
        public static partial void ResumeRequested(ILogger logger, bool playing, bool paused);

        /// <summary>Records a finished cutscene being handed to the game.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="url">Movie that finished.</param>
        /// <param name="listener">Type of the controller told.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Finished {Url}; notifying {Listener}")]
        public static partial void Finished(ILogger logger, string url, string listener);

        /// <summary>Reports a cutscene that finished with nothing listening for it.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="url">Movie that finished.</param>
        /// <remarks>
        /// The listener is what takes the movie view down, so a finish nobody hears leaves the
        /// screen black with nothing left to change it.
        /// </remarks>
        [LoggerMessage(Level = LogLevel.Warning, Message = "Finished {Url} with no listener; nothing will leave the movie view")]
        public static partial void FinishedUnobserved(ILogger logger, string url);
    }
}
