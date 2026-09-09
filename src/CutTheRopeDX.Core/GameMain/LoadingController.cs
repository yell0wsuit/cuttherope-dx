using System.Diagnostics;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Controller for the pack loading screen and transition back to the root controller.
    /// </summary>
    internal sealed class LoadingController : ViewController, IResourceMgrDelegate
    {
        /// <summary>The loading label, centered in the view.</summary>
        private readonly Text loadingText;

        /// <summary>
        /// Initializes a loading controller and its loading label.
        /// </summary>
        /// <param name="parent">Parent view controller.</param>
        public LoadingController(ViewController parent)
            : base(parent)
        {
            LoadingView loadingView = new();
            AddViewwithID(loadingView, 0);
            Text text = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
            text.SetAlignment(2);
            text.SetStringandWidth(Application.GetString("LOADING"), 300f);
            text.anchor = text.parentAnchor = 18;
            _ = loadingView.AddChild(text);
            loadingText = text;
        }

        /// <inheritdoc />
        protected override void Relayout(ViewportLayoutSnapshot snapshot)
        {
            base.Relayout(snapshot);

            // Center-anchored, so it already stays centered as it scales - no position
            // correction needed the way an edge-anchored element requires.
            loadingText.scaleX = loadingText.scaleY = FittedScale;
        }

        /// <inheritdoc />
        public override void Update(float t)
        {
            base.Update(t);
            shownSeconds += t;

            // Wait for animation to complete before transitioning
            if (resourcesLoaded && shownSeconds >= MinimumDisplaySeconds)
            {
                LoadingView loadingView = (LoadingView)GetView(0);
                if (loadingView.IsAnimationComplete())
                {
                    Application.SharedRootController().SetViewTransition(4);
                    Deactivate();
                    resourcesLoaded = false; // Reset for next time
                }
            }
        }

        /// <inheritdoc />
        public override void Activate()
        {
            AndroidAPI.ShowBanner();
            base.Activate();
            resourcesLoaded = false; // Reset flag when activating
            shownSeconds = 0f;
            loadStartedTicks = Stopwatch.GetTimestamp();
            ((LoadingView)GetView(0)).game = nextController == 0;
            ShowView(0);
        }

        /// <inheritdoc />
        public override void DeactivateImmediately()
        {
            resourcesLoaded = false; // Clear state
            base.DeactivateImmediately();
        }

        /// <summary>
        /// Marks pending resources as loaded so the controller can transition after the loading animation finishes.
        /// </summary>
        public void AllResourcesLoaded()
        {
            // Just set flag - Update() will handle transition after animation completes
            resourcesLoaded = true;

            // The screen stays up for its minimum display time after this, so this is when the
            // level was actually ready rather than when the player saw it.
            if (nextController == 0)
            {
                double elapsedMs = Stopwatch.GetElapsedTime(loadStartedTicks).TotalMilliseconds;
                ILogger logger = Log.For(LogCategories.ContentXml);
                string memory = MemoryReport.Describe();
                LoadingControllerLog.LevelReady(logger, elapsedMs, memory);
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                resourcesLoaded = false;
                nextController = 0;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Shortest time the loading screen stays up.
        /// </summary>
        private const float MinimumDisplaySeconds = 0.5f;

        /// <summary>Controller ID to activate after the loading screen completes.</summary>
        public int nextController;

        /// <summary>When the loading screen came up, for the level-ready report.</summary>
        private long loadStartedTicks = Stopwatch.GetTimestamp();

        /// <summary>Whether the resource manager has finished loading the requested resources.</summary>
        private bool resourcesLoaded;

        /// <summary>Seconds the loading screen has been visible since the last activation.</summary>
        private float shownSeconds;

        /// <summary>
        /// View identifiers owned by the loading controller.
        /// </summary>
        private enum ViewID
        {
            /// <summary>Loading view identifier.</summary>
            VIEW_LOADING
        }
    }

    /// <summary>Log messages for the loading screen.</summary>
    internal static partial class LoadingControllerLog
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Level ready in {ElapsedMs:F1} ms; {Memory}")]
        public static partial void LevelReady(ILogger logger, double elapsedMs, string memory);
    }
}
