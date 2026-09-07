using System;

using CutTheRopeDX.Framework.Platform;

namespace CutTheRopeDX.Framework.Media
{
    /// <summary>
    /// Chooses the video player this build can actually use.
    /// </summary>
    /// <remarks>
    /// Which backends exist is a compile-time property of the desktop build, so the choice is made
    /// here and installed into <see cref="PlatformServices.VideoPlayerFactory"/>; Core's
    /// <see cref="MovieMgr"/> never learns which one it got. Both desktop hosts share this, so the
    /// interim SDL host and the legacy host cannot end up playing movies through different
    /// decoders.
    /// </remarks>
    internal static class DesktopVideoPlayerFactory
    {
        /// <summary>Constructs the selected backend, or the no-op stub when none is available.</summary>
        /// <returns>The selected <see cref="IVideoPlayer"/> implementation.</returns>
        public static IVideoPlayer Create()
        {
            bool hasAvFoundation =
#if MACOS_AVFOUNDATION
                true;
#else
                false;
#endif

            bool hasFfmpeg =
#if FFMPEG_BACKEND
                true;
#else
                false;
#endif

            VideoPlayerBackend backend = VideoPlayerBackendSelector.Select(
                isMac: OperatingSystem.IsMacOS(),
                isMac26OrLater: OperatingSystem.IsMacOSVersionAtLeast(26),
                hasAvFoundation: hasAvFoundation,
                hasFfmpeg: hasFfmpeg);

#pragma warning disable IDE0010, IDE0066
            switch (backend)
            {
#if MACOS_AVFOUNDATION
                case VideoPlayerBackend.AVFoundation:
                    return new VideoPlayerAVFoundation();
#endif
#if FFMPEG_BACKEND
                case VideoPlayerBackend.Ffmpeg:
                    return new VideoPlayerFFmpeg();
#endif
                default:
                    return new VideoPlayerMonoGame();
            }
#pragma warning restore IDE0010, IDE0066
        }
    }
}
