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

            // A backend this build did not compile in has an empty case and falls through to the stub.
            switch (backend)
            {
                case VideoPlayerBackend.AVFoundation:
#if MACOS_AVFOUNDATION
                    return new VideoPlayerAVFoundation();
#endif
                case VideoPlayerBackend.Ffmpeg:
#if FFMPEG_BACKEND
                    return new VideoPlayerFFmpeg();
#endif
                case VideoPlayerBackend.None:
                default:
                    return new VideoPlayerNone();
            }
        }
    }
}
