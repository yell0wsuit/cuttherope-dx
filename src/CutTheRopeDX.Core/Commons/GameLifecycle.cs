using System;
using System.Collections.Generic;
using System.Diagnostics;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.Helpers;

using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.Commons
{
    /// <summary>
    /// Bridges the game's shared runtime to the platform rendering and lifecycle callbacks.
    /// </summary>
    internal sealed class GameLifecycle : FrameworkTypes
    {
        /// <summary>
        /// Marks the rendering surface as created so the runtime can finish initialization on the next frame.
        /// </summary>
        public static void OnSurfaceCreated()
        {
            if (state == 0)
            {
                state = 1;
            }
        }

        /// <summary>
        /// The sole entry point for a surface size change. Publishes the viewport snapshot, which
        /// every screen metric is then read from, so no consumer can observe a half-updated state.
        /// </summary>
        /// <param name="width">The new surface width in pixels.</param>
        /// <param name="height">The new surface height in pixels.</param>
        /// <param name="devicePixelRatio">Physical pixels per logical pixel on the host surface.</param>
        public static void OnSurfaceChanged(
            int width,
            int height,
            float devicePixelRatio = 1f)
        {
            bool changed = ScreenPresentation.Instance.SetSurfaceSize(
                width, height, devicePixelRatio);
            if (changed)
            {
                Application.ExistingRootController()?.RelayoutTree(ScreenPresentation.Instance.Snapshot);
            }
        }

        /// <summary>
        /// Pauses rendering and runtime subsystems when the platform host is paused.
        /// </summary>
        public static void OnPause()
        {
            if (state is 2 or 5)
            {
                PauseRuntime();
                state = 3;
            }
        }

        /// <summary>
        /// Handles playback completion notifications.
        /// </summary>
        public static void OnPlaybackFinished()
        {
        }

        /// <summary>
        /// Marks the renderer as being in playback mode.
        /// </summary>
        public static void OnPlaybackStarted()
        {
            state = 5;
        }

        /// <summary>
        /// Schedules the runtime to resume after the platform host becomes active again.
        /// </summary>
        public static void OnResume()
        {
            if (state == 3)
            {
                state = 4;
                onResumeTimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                DRAW_NOTHING = false;
            }
        }

        /// <summary>
        /// Tears down the runtime when the platform surface is being destroyed.
        /// </summary>
        public static void OnDestroy()
        {
            if (state != 1)
            {
                DestroyRuntime();
                state = 1;
            }
        }

        /// <summary>
        /// Advances the game runtime using the fixed frame delta expected by the original renderer.
        /// </summary>
        public static void Update()
        {
            Tick(16f);
        }

        /// <summary>
        /// Renders a frame or clears the backbuffer when rendering is currently suspended.
        /// </summary>
        public static void OnDrawFrame()
        {
            bool didRenderFrame = false;
            if (!DRAW_NOTHING && state != 0)
            {
                if (state == 1)
                {
                    state = 2;
                }
                if (state != 3)
                {
                    if (state == 4)
                    {
                        if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - onResumeTimeStamp >= 500L)
                        {
                            ResumeRuntime();
                            RenderFrame();
                            didRenderFrame = true;
                            state = 2;
                        }
                    }
                    else if (state == 2)
                    {
                        long timestamp = Stopwatch.GetTimestamp();
                        playedTicks += DELTA_NANOS;
                        if (timestamp - playedTicks < DELTA_NANOS_THRES)
                        {
                            if (playedTicks < timestamp)
                            {
                                playedTicks = timestamp;
                            }
                        }
                        else if (state == 2)
                        {
                            playedTicks += DELTA_NANOS;
                            if (timestamp - playedTicks > DELTA_NANOS_THRES)
                            {
                                playedTicks = timestamp - DELTA_NANOS_THRES;
                            }
                        }
                        if (state == 2)
                        {
                            RenderFrame();
                            didRenderFrame = true;
                        }
                    }
                }
            }
            if (!didRenderFrame)
            {
                try
                {
                    Renderer.SetClearColor(Color.Black);
                    Renderer.Clear(0);
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// Initializes the shared application runtime with the selected language.
        /// </summary>
        /// <param name="language">The language to assign to the runtime before launch.</param>
        public static void InitRuntime(Language language)
        {
            if (gApp != null)
            {
                ILogger logger = Log.For(LogCategories.Application);
                GameLifecycleLog.AlreadyInitialized(logger);
                return;
            }
            LanguageHelper.Current = language;
            FmInit();
            gApp = new Application();
            gApp.ApplicationDidFinishLaunching();
        }

        /// <summary>
        /// Destroys the shared application runtime and saves any pending preferences.
        /// </summary>
        public static void DestroyRuntime()
        {
            if (gApp == null)
            {
                ILogger logger = Log.For(LogCategories.Application);
                GameLifecycleLog.NotInitialized(logger);
                return;
            }
            Application.SharedSoundMgr().StopAllSounds();
            Preferences.RequestSave();
            gApp = null;
            gPaused = false;
        }

        /// <summary>
        /// Suspends audio, movie playback, textures, and app state.
        /// </summary>
        public static void PauseRuntime()
        {
            if (!gPaused)
            {
                SoundMgr.Pause();
                Application.SharedMovieMgr().Pause();
                gPaused = true;
                Application.ApplicationWillResignActive();
                Texture2D.SuspendAll();
            }
        }

        /// <summary>
        /// Resumes audio, movie playback, textures, and app state after a pause.
        /// </summary>
        public static void ResumeRuntime()
        {
            if (gPaused)
            {
                SoundMgr.Unpause();
                Application.SharedMovieMgr().Resume();
                Texture2D.SuspendAll();
                Texture2D.ResumeAll();
                gPaused = false;
                Application.ApplicationDidBecomeActive();
            }
        }

        /// <summary>
        /// Clears the frame and delegates drawing to the root controller.
        /// </summary>
        public static void RenderFrame()
        {
            Renderer.SetClearColor(Color.Black);
            Renderer.Clear(0);
            if (gApp != null)
            {
                Application.SharedRootController().PerformDraw();
            }
        }

        /// <summary>
        /// Converts a view-space X coordinate into game-space coordinates.
        /// </summary>
        /// <param name="x">The view-space X coordinate.</param>
        /// <returns>The transformed game-space X coordinate.</returns>
        public static float TransformX(float x)
        {
            return ScreenPresentation.Instance.TransformViewToGameX(x);
        }

        /// <summary>
        /// Converts a view-space Y coordinate into game-space coordinates.
        /// </summary>
        /// <param name="y">The view-space Y coordinate.</param>
        /// <returns>The transformed game-space Y coordinate.</returns>
        public static float TransformY(float y)
        {
            return ScreenPresentation.Instance.TransformViewToGameY(y);
        }

        /// <summary>
        /// Forwards touch input from the platform layer to the shared canvas.
        /// </summary>
        /// <param name="touches">The touch locations reported for the current frame.</param>
        public static void ProcessTouches(IList<TouchLocation> touches)
        {
            if (touches.Count > 0)
            {
                Application.SharedCanvas().TouchesEndedwithEvent(touches);
                Application.SharedCanvas().TouchesBeganwithEvent(touches);
                Application.SharedCanvas().TouchesMovedwithEvent(touches);
            }
        }

        /// <summary>
        /// Forwards the back-button action to the shared canvas.
        /// </summary>
        /// <returns><see langword="true"/> if the canvas handled the action; otherwise, <see langword="false"/>.</returns>
        public static bool BackPressed()
        {
            GLCanvas gLCanvas = Application.SharedCanvas();
            return gLCanvas != null && gLCanvas.BackButtonPressed();
        }

        /// <summary>
        /// Forwards the menu-button action to the shared canvas.
        /// </summary>
        /// <returns><see langword="true"/> if the canvas handled the action; otherwise, <see langword="false"/>.</returns>
        public static bool MenuPressed()
        {
            GLCanvas gLCanvas = Application.SharedCanvas();
            return gLCanvas != null && gLCanvas.MenuButtonPressed();
        }

        /// <summary>
        /// Advances timers and the root controller by the specified frame delta.
        /// </summary>
        /// <param name="delta">The frame delta in milliseconds.</param>
        public static void Tick(float delta)
        {
            if (gApp != null && !gPaused)
            {
                float deltaSeconds = delta / 1000f;
                TimerManager.Update(deltaSeconds);
                Application.SharedRootController().PerformTick(deltaSeconds);
            }
        }

        /// <summary>
        /// Tracks the current renderer lifecycle state.
        /// </summary>
        private static int state;

        /// <summary>
        /// Stores the timestamp recorded when a resume was requested.
        /// </summary>
        private static long onResumeTimeStamp;

        /// <summary>
        /// Accumulates the simulated playback timeline used for frame pacing.
        /// </summary>
        private static long playedTicks;

        /// <summary>
        /// The nominal frame duration in nanoseconds for the fixed-step renderer.
        /// </summary>
        private static readonly long DELTA_NANOS = 18181818L;

        /// <summary>
        /// The maximum timing drift tolerated before the pacing timeline is clamped.
        /// </summary>
        private static readonly long DELTA_NANOS_THRES = (long)(DELTA_NANOS * 0.35);

        /// <summary>
        /// Indicates whether frame rendering should be skipped temporarily.
        /// </summary>
        private static bool DRAW_NOTHING;

        /// <summary>
        /// Holds the shared application instance owned by the renderer bridge.
        /// </summary>
        private static Application gApp;

        /// <summary>
        /// Indicates whether the runtime is currently paused.
        /// </summary>
        private static bool gPaused;
    }

    /// <summary>Log messages for the shared runtime's lifecycle.</summary>
    internal static partial class GameLifecycleLog
    {
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Init requested while the runtime is already running; ignored.")]
        public static partial void AlreadyInitialized(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Destroy requested while no runtime is running; ignored.")]
        public static partial void NotInitialized(ILogger logger);
    }
}
