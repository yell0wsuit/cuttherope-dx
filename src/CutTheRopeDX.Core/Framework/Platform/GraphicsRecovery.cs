using CutTheRopeDX.Commons;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.Framework.Platform
{
    /// <summary>What recovery leaves the running application doing once a device is back.</summary>
    internal enum GraphicsRecoveryStance
    {
        /// <summary>
        /// Resume everything, then offer the pause menu. Gameplay opens it; a screen without one
        /// carries on exactly where it was.
        /// </summary>
        ResumeAndPause,

        /// <summary>
        /// Leave a cutscene stopped where it was. Frames kept decoding through the blackout, so
        /// resuming on its own would jump the picture past everything that was missed.
        /// </summary>
        HoldMovie,
    }

    /// <summary>What a pointer press should do to the cutscene on screen.</summary>
    internal enum MoviePressAction
    {
        /// <summary>Nothing; the press began before the cutscene did.</summary>
        Ignore,

        /// <summary>Start a cutscene that recovery left held.</summary>
        Resume,

        /// <summary>Skip the cutscene.</summary>
        Skip,
    }

    /// <summary>What a device loss cost, and how to finish recovering from it.</summary>
    /// <param name="Stance">How the application should be left once a device is back.</param>
    /// <param name="DroppedCaptures">Captured frames released because nothing can rebuild them.</param>
    internal readonly record struct GraphicsRecoveryPlan(
        GraphicsRecoveryStance Stance, int DroppedCaptures);

    /// <summary>What one recovery had to rebuild.</summary>
    /// <param name="ReloadedAssets">Textures loaded again from their content path.</param>
    /// <param name="DroppedCaptures">Captured frames that nothing could rebuild.</param>
    internal readonly record struct GraphicsRecoveryReport(int ReloadedAssets, int DroppedCaptures);

    /// <summary>
    /// The application half of recovering from a lost graphics device: what to tear down before
    /// the device is replaced, and what to rebuild once one is running again.
    /// </summary>
    /// <remarks>
    /// Nothing here touches the simulation. The physics bodies, timers, progress and menu state
    /// all live in ordinary managed memory that no graphics device owns, so a device that comes
    /// and goes leaves them exactly as they were; recovery's whole job is the resources that did
    /// belong to it, plus stopping the clock while there is nothing to draw into.
    /// </remarks>
    internal static class GraphicsRecovery
    {
        /// <summary>Chooses how to leave the application once a device is back.</summary>
        /// <param name="movieActive">Whether a cutscene was playing when the device went.</param>
        public static GraphicsRecoveryStance StanceFor(bool movieActive)
        {
            return movieActive ? GraphicsRecoveryStance.HoldMovie : GraphicsRecoveryStance.ResumeAndPause;
        }

        /// <summary>Chooses what a press does to the cutscene on screen.</summary>
        /// <param name="armed">Whether the pointer has come up since the cutscene started.</param>
        /// <param name="moviePaused">Whether the cutscene is being held.</param>
        /// <remarks>
        /// A held cutscene has to be started deliberately, and the only deliberate action a
        /// cutscene offers is a press, so the first one resumes rather than skips. The press that
        /// opened the cutscene is not one of those: it was already down before the first frame,
        /// which is what the arming flag records.
        /// </remarks>
        public static MoviePressAction PressAction(bool armed, bool moviePaused)
        {
            return !armed ? MoviePressAction.Ignore
                : moviePaused ? MoviePressAction.Resume
                : MoviePressAction.Skip;
        }

        /// <summary>
        /// Stops the application and releases everything the failing device owned. Runs while that
        /// device is still alive.
        /// </summary>
        /// <returns>What the loss cost, to hand back to <see cref="Complete"/>.</returns>
        /// <remarks>
        /// Textures loaded from a file are released by the cache that owns them, so they are only
        /// unhooked here. A capture belongs to nobody else, so it is released here or not at all.
        /// </remarks>
        public static GraphicsRecoveryPlan Begin()
        {
            GraphicsRecoveryStance stance = StanceFor(Application.SharedMovieMgr().IsPlaying());

            // The ordinary pause path, so a device loss stops the same things a lost window does:
            // audio, the cutscene, and the root controller's input routing and clock.
            CtrRenderer.Java_com_zeptolab_ctr_CtrRenderer_nativePause();

            int dropped = Application.SharedRootController().DropTransitionCaptures();
            foreach (CTRTexture2D texture in CTRTexture2D.Registered())
            {
                if (texture._resName != null)
                {
                    texture.textureHandle_ = null;
                    continue;
                }

                if (texture.textureHandle_ != null)
                {
                    texture.textureHandle_.Dispose();
                    texture.textureHandle_ = null;
                    dropped++;
                }
            }

            return new GraphicsRecoveryPlan(stance, dropped);
        }

        /// <summary>
        /// Rebuilds what a replacement device needs and starts the application again.
        /// </summary>
        /// <param name="plan">What <see cref="Begin"/> reported.</param>
        /// <returns>What was rebuilt.</returns>
        /// <remarks>
        /// Only textures the running game still references are loaded. A texture holds the content
        /// path it came from, so the list of live textures is the list of assets that matter, and
        /// everything the caches happened to be holding for a screen nobody is on stays unloaded.
        /// </remarks>
        public static GraphicsRecoveryReport Complete(GraphicsRecoveryPlan plan)
        {
            int reloaded = 0;
            foreach (CTRTexture2D texture in CTRTexture2D.Registered())
            {
                if (texture._resName == null)
                {
                    continue;
                }

                texture.textureHandle_ = AssetPlatform.Current.ImageTexture(texture._resName);
                reloaded++;
            }

            CtrRenderer.Java_com_zeptolab_ctr_CtrRenderer_nativeResume();
            if (plan.Stance == GraphicsRecoveryStance.HoldMovie)
            {
                // Resuming unpaused the cutscene along with everything else, so it is stopped
                // again here rather than being left out of the ordinary resume path.
                Application.SharedMovieMgr().Pause();
            }
            else
            {
                // Whatever is on screen decides what this means. Live gameplay opens its pause
                // menu; a menu has no paused state and carries on exactly where it was, and a game
                // that was already paused when the device went stays paused.
                _ = Application.SharedRootController().EnsurePaused();
            }

            return new GraphicsRecoveryReport(reloaded, plan.DroppedCaptures);
        }
    }
}
