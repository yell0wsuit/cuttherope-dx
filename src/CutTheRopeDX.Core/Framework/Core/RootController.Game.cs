using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

using CutTheRopeDX.Commons;
using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Helpers;

using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.Framework.Core
{
    // Game lifecycle: startup → menu → loading → gameplay, resource loading/unloading across
    // transitions, and background prefetch of box-level resources.
    internal partial class RootController
    {
        /// <summary>
        /// Stub for analytics event logging.
        /// </summary>
        /// <param name="_">Event name.</param>
        /// <remarks>
        /// No-op on PC.
        /// </remarks>
        public static void LogEvent(string _)
        {
        }

        /// <summary>
        /// Gets the parsed gameplay map XML currently cached on the root controller.
        /// </summary>
        public XElement Map { get; set; }

        /// <summary>
        /// Gets the current map filename tracked for reload and transition flows.
        /// </summary>
        public string MapName { get; set; }

        /// <summary>
        /// Synchronously ensures the resources required by a map are loaded, then stores the map as current.
        /// </summary>
        /// <param name="map">The parsed map XML to prepare.</param>
        /// <param name="newMapName">Optional map filename to persist on the root controller.</param>
        public void PrepareMapAndEnsureResources(XElement map, string newMapName)
        {
            if (map == null)
            {
                return;
            }

            StopGameplayPrefetch();

            long startedTicks = Stopwatch.GetTimestamp();
            string[] levelResources = LevelResourceScanner.GetRequiredResources(map);
            TrackSessionResources(levelResources);

            ResourceMgr resourceMgr = Application.SharedResourceMgr();
            resourceMgr.InitLoading();
            resourceMgr.LoadPack(levelResources);
            resourceMgr.LoadImmediately();

            Map = map;
            if (!string.IsNullOrWhiteSpace(newMapName))
            {
                MapName = newMapName;
            }

            double elapsedMs = Stopwatch.GetElapsedTime(startedTicks).TotalMilliseconds;
            ILogger logger = Log.For(LogCategories.ContentXml);
            string memory = MemoryReport.Describe();
            RootControllerLog.LevelReady(logger, Pack, Level, newMapName, elapsedMs, memory);

            StartBoxResourceScanIfNeeded();
            QueueOrPollBoxPrefetch();
        }

        /// <summary>Stub for setting a maps dictionary.</summary>
        /// <param name="_">Maps dictionary.</param>
        /// <remarks>
        /// No-op on PC.
        /// </remarks>
        public static void SetMapsList(Dictionary<string, XElement> _)
        {
        }

        /// <summary>Gets the current pack (box group) index.</summary>
        public int Pack { get; set; }

        /// <summary>
        /// Creates the game's root controller with startup resources loaded and the startup child
        /// controller (or, for a custom-level session, the loading controller) added.
        /// </summary>
        /// <returns>The new root controller.</returns>
        public static RootController CreateGameRoot()
        {
            RootController root = new(null);
            root.LoadStartup();
            return root;
        }

        /// <summary>
        /// Loads startup resources and adds the first child controller.
        /// </summary>
        private void LoadStartup()
        {
            Map = null;
            ResourceMgr resourceMgr = Application.SharedResourceMgr();
            resourceMgr.InitLoading();
            resourceMgr.LoadPack(PackStartup);
            resourceMgr.LoadImmediately();

            if (CustomLevelSession.IsActive)
            {
                Pack = 0;
                Level = 0;
                LoadingController customLoading = new(this);
                AddChildwithID(customLoading, 2);
                viewTransition = -1;
                return;
            }

            StartupController startupController = new(this);
            AddChildwithID(startupController, 0);
            viewTransition = -1;
        }

        /// <inheritdoc />
        public override void Activate()
        {
            _ = Preferences.IsFirstLaunch();
            base.Activate();

            if (CustomLevelSession.IsActive)
            {
                BeginCustomLevelLoad();
            }
            else
            {
                ActivateChild(0);
            }

            // A priming draw outside the draw loop; headless runs have no device to draw with.
            if (Renderer.IsAvailable)
            {
                GLCanvas.BeforeRender();
                ActiveChild().ActiveView().Draw();
            }
        }

        /// <summary>
        /// Gets the resources loaded for the current gameplay session.
        /// </summary>
        public ISet<string> SessionResources => sessionResources;

        /// <summary>
        /// Frees the previous level's resources, loads the edited level's resources through the
        /// loading screen, and re-enters gameplay.
        /// </summary>
        /// <param name="resourceMgr">Shared resource manager.</param>
        private void ReloadCustomLevelThroughLoadingScreen(ResourceMgr resourceMgr)
        {
            DeleteChild(3);

            string[] levelResources = LevelResourceScanner.GetRequiredResources(Map);
            resourceMgr.FreePack([.. sessionResources]);
            sessionResources.Clear();
            TrackSessionResources(levelResources);

            resourceMgr.resourcesDelegate = (LoadingController)GetChild(2);
            resourceMgr.InitLoading();
            resourceMgr.LoadPack(levelResources);
            resourceMgr.StartLoading();
            ILogger logger = Log.For(LogCategories.ContentXml);
            string mapName = MapName;
            RootControllerLog.LevelLoadStarted(logger, Pack, Level, mapName, levelResources.Length);
            ((LoadingController)GetChild(2)).nextController = 0;
            ActivateChild(2);
        }

        /// <summary>
        /// Loads gameplay resources for the externally supplied level and enters the loading screen.
        /// </summary>
        private void BeginCustomLevelLoad()
        {
            ResourceMgr resourceMgr = Application.SharedResourceMgr();
            resourceMgr.resourcesDelegate = (LoadingController)GetChild(2);
            ResetGameplayResourceSession();
            EnsureCurrentMapLoaded();
            string[] levelResources = LevelResourceScanner.GetRequiredResources(Map);
            TrackSessionResources(levelResources);
            resourceMgr.InitLoading();
            resourceMgr.LoadPack(PackGame);
            resourceMgr.LoadPack(PackConfig.GetBoxBackgrounds(Pack));
            resourceMgr.LoadPack(levelResources);
            resourceMgr.StartLoading();
            ILogger logger = Log.For(LogCategories.ContentXml);
            string mapName = MapName;
            RootControllerLog.LevelLoadStarted(logger, Pack, Level, mapName, levelResources.Length);
            ((LoadingController)GetChild(2)).nextController = 0;
            ActivateChild(2);
        }

        /// <summary>Removes the menu child controller and frees menu resources.</summary>
        public void DeleteMenu()
        {
            ResourceMgr resourceMgr = Application.SharedResourceMgr();
            DeleteChild(1);
            Application.SharedMovieMgr().delegateMovieMgrDelegate = null;
            resourceMgr.FreePack(PackMenu);
        }

        /// <summary>Disabling Game Center.</summary>
        /// <remarks>
        /// No-op on PC.
        /// </remarks>
        public static void DisableGameCenter()
        {
        }

        /// <summary>Enabling Game Center.</summary>
        /// <remarks>
        /// No-op on PC.
        /// </remarks>
        public static void EnableGameCenter()
        {
        }

        /// <inheritdoc />
        public override void OnChildDeactivated(int n)
        {
            base.OnChildDeactivated(n);
            ResourceMgr resourceMgr = Application.SharedResourceMgr();
            switch (n)
            {
                case 0:
                    {
                        SetViewTransition(4);
                        PrebuildMenuControllers();
                        LoadingController loading = prebuiltLoading;
                        prebuiltLoading = null;
                        AddChildwithID(loading, 2);
                        MenuController menu = prebuiltMenu;
                        prebuiltMenu = null;
                        AddChildwithID(menu, 1);
                        DeleteChild(0);
                        resourceMgr.FreePack(PackStartup);
                        menu.viewToShow = 0;
                        if (Preferences.GetBooleanForKey("PREFS_GAME_CENTER_ENABLED"))
                        {
                            EnableGameCenter();
                        }
                        else
                        {
                            DisableGameCenter();
                        }
                        if (Preferences.GetBooleanForKey("IAP_BANNERS"))
                        {
                            AndroidAPI.DisableBanners();
                        }
                        ILogger logger = Log.For(LogCategories.Application);
                        RootControllerLog.ShowingMenu(logger);
                        ActivateChild(1);
                        //Show menu presence after loading screen
                        PlatformServices.RichPresence?.MenuPresence();
                        return;
                    }
                case 1:
                    {
                        DeleteMenu();
                        resourceMgr.resourcesDelegate = (LoadingController)GetChild(2);
                        ResetGameplayResourceSession();
                        EnsureCurrentMapLoaded();
                        string[] levelResources = LevelResourceScanner.GetRequiredResources(Map);
                        TrackSessionResources(levelResources);
                        StartBoxResourceScanIfNeeded();
                        resourceMgr.InitLoading();
                        resourceMgr.LoadPack(PackGame);
                        resourceMgr.LoadPack(PackConfig.GetBoxBackgrounds(Pack));
                        resourceMgr.LoadPack(levelResources);
                        resourceMgr.StartLoading();
                        ((LoadingController)GetChild(2)).nextController = 0;
                        ActivateChild(2);
                        return;
                    }
                case 2:
                    {
                        int nextController = ((LoadingController)GetChild(2)).nextController;
                        long buildStartedTicks = Stopwatch.GetTimestamp();
                        if (nextController == 0)
                        {
                            SetShowGreeting(true);
                            GameController game = new(this);
                            AddChildwithID(game, 3);
                            ActivateChild(3);
                            ILogger gameBuildLogger = Log.For(LogCategories.Application);
                            double gameBuildMs = Stopwatch.GetElapsedTime(buildStartedTicks).TotalMilliseconds;
                            RootControllerLog.ControllerBuilt(gameBuildLogger, "game", gameBuildMs);
                            QueueOrPollBoxPrefetch();
                            return;
                        }
                        if (nextController - 1 > 3)
                        {
                            return;
                        }
                        MenuController menu = new(this);
                        AddChildwithID(menu, 1);
                        // A menu opening on level select shows the current box's cover at once. That
                        // cover is still loaded, from before the level or from the menu's own batch,
                        // so freeing it here would only have the menu decode it again in the frame
                        // that builds it.
                        int keptCoverPack = nextController is 2 or 4 ? Pack : -1;
                        int packCount = Preferences.GetPacksCount();
                        List<string[]> covers = [];
                        for (int i = 0; i < packCount; i++)
                        {
                            if (i != keptCoverPack)
                            {
                                covers.Add(PackConfig.GetBoxCovers(i));
                            }
                        }
                        resourceMgr.FreePacks(covers);
                        if (IS_WVGA)
                        {
                            SetViewTransition(4);
                        }
                        if (nextController == 1)
                        {
                            menu.viewToShow = 0;
                        }
                        if (nextController is 2 or 4)
                        {
                            menu.viewToShow = 6;
                        }
                        if (nextController == 3)
                        {
                            menu.viewToShow = Pack < Preferences.GetPacksCount() - 1 ? 5 : (PackConfig.OutroVideo != null ? 7 : 5);
                        }
                        ActivateChild(1);
                        if (nextController == 3)
                        {
                            menu.ShowNextPack();
                        }
                        ILogger menuBuildLogger = Log.For(LogCategories.Application);
                        double menuBuildMs = Stopwatch.GetElapsedTime(buildStartedTicks).TotalMilliseconds;
                        RootControllerLog.ControllerBuilt(menuBuildLogger, "menu", menuBuildMs);
                        return;
                    }
                case 3:
                    {
                        SaveMgr.Backup();
                        GameController gameController = (GameController)GetChild(3);
                        int exitCode = gameController.exitCode;
                        _ = (GameScene)gameController.GetView(0).GetChild(0);

                        if (exitCode == GameController.EXIT_CODE_CUSTOM_RELOAD)
                        {
                            ReloadCustomLevelThroughLoadingScreen(resourceMgr);
                            return;
                        }

                        if (exitCode <= 2)
                        {
                            StopGameplayPrefetch();
                            DeleteChild(3);
                            List<string[]> gameplayPacks = [PackGame, [.. sessionResources]];
                            sessionResources.Clear();
                            int packCount = Preferences.GetPacksCount();
                            for (int i = 0; i < packCount; i++)
                            {
                                gameplayPacks.Add(PackConfig.GetBoxBackgrounds(i));
                            }
                            resourceMgr.FreePacks(gameplayPacks);
                            resourceMgr.resourcesDelegate = (LoadingController)GetChild(2);
                            int menuNextController = exitCode != 0 ? exitCode != 1 ? 3 : 2 : 1;
                            resourceMgr.InitLoading();
                            resourceMgr.LoadPack(PackMenu);
                            if (menuNextController == 2)
                            {
                                // The menu opens on level select, which shows this box's cover at
                                // once. Loading it with the menu keeps it out of the frame that
                                // builds the menu; one still loaded from before costs nothing.
                                resourceMgr.LoadPack(PackConfig.GetBoxCovers(Pack));
                            }
                            resourceMgr.StartLoading();
                            LoadingController loadingController = (LoadingController)GetChild(2);
                            loadingController.nextController = menuNextController;
                            ActivateChild(2);
                            //Show menu presence on exit to menu
                            PlatformServices.RichPresence?.MenuPresence();
                        }
                        return;
                    }
                default:
                    return;
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopGameplayPrefetch();
                Map = null;
                MapName = null;
            }
            base.Dispose(disposing);
        }

        /// <summary>Map validation.</summary>
        /// <remarks>
        /// No-op code.
        /// </remarks>
        public static void CheckMapIsValid()
        {
        }

        //public static bool IsHacked()
        //{
        //    return false;
        //}

        //public static void SetHacked()
        //{
        //}

        /// <summary>Sets the Chillingo's Crystal overlay state on the shared root controller.</summary>
        /// <param name="b">Whether the Crystal overlay is active.</param>
        public static void SetInCrystal(bool b)
        {
            Application.SharedRootController().inCrystal = b;
        }

        /// <summary>Stub for opening the full version store page.</summary>
        /// <remarks>
        /// No-op code.
        /// </remarks>
        public static void OpenFullVersionPage()
        {
        }

        /// <summary>Gets the current box index.</summary>
        public int Box { get; set; }

        /// <summary>Gets the current level index within the active box.</summary>
        public int Level { get; set; }

        /// <summary>Sets whether the level picker is active.</summary>
        /// <param name="p">Whether the level picker should be shown.</param>
        public void SetPicker(bool p)
        {
            picker = p;
        }

        /// <summary>Gets whether the level picker is active.</summary>
        /// <returns><see langword="true"/> if the level picker is shown; otherwise <see langword="false"/>.</returns>
        public bool IsPicker()
        {
            return picker;
        }

        /// <summary>Sets whether survival mode is active.</summary>
        /// <param name="s">Whether survival mode is enabled.</param>
        public void SetSurvival(bool s)
        {
            survival = s;
        }

        /// <summary>Gets whether survival mode(?) is active.</summary>
        /// <returns><see langword="true"/> if survival mode is enabled; otherwise <see langword="false"/>.</returns>
        public bool IsSurvival()
        {
            return survival;
        }

        /// <summary>Gets whether the Om Nom greeting animation should play on the next level start.</summary>
        /// <returns><see langword="true"/> if the greeting should be shown.</returns>
        public static bool IsShowGreeting()
        {
            return Application.SharedRootController().showGreeting;
        }

        /// <summary>Sets whether the Om Nom greeting animation should play on the next level start.</summary>
        /// <param name="s">Whether to show the greeting.</param>
        public static void SetShowGreeting(bool s)
        {
            Application.SharedRootController().showGreeting = s;
        }

        /// <summary>Destroys and re-creates the loading controller child (slot 2).</summary>
        internal void RecreateLoadingController()
        {
            DeleteChild(2);
            LoadingController c = new(this);
            AddChildwithID(c, 2);
        }

        /// <summary>
        /// Loads the current map XML from disk when only the pack/level identity is known.
        /// </summary>
        private void EnsureCurrentMapLoaded()
        {
            if (Map != null)
            {
                return;
            }

            if (CustomLevelSession.IsActive)
            {
                if (CustomLevelFile.TryLoad(CustomLevelSession.LevelPath, out XElement customMap, out string error))
                {
                    Map = customMap;
                    MapName = CustomLevelSession.LevelPath;
                }
                else
                {
                    Console.Error.WriteLine(error);
                    ILogger rejectedLogger = Log.For(LogCategories.Playtest);
                    PlaytestLog.LevelRejected(rejectedLogger, error);
                }

                return;
            }

            string currentMapName = MapName;
            if (string.IsNullOrWhiteSpace(currentMapName) && Pack >= 0 && Level >= 0 && Pack < PackConfig.PackCount && Level < PackConfig.GetLevelCount(Pack))
            {
                currentMapName = LevelsList.LEVEL_NAMES[Pack, Level];
                MapName = currentMapName;
            }

            if (string.IsNullOrWhiteSpace(currentMapName))
            {
                return;
            }

            Map = ContentPaths.LoadXml(Path.Combine(ContentPaths.MapsDirectory, currentMapName));
        }

        /// <summary>
        /// Adds resource identifiers to the set that will be freed when gameplay ends.
        /// </summary>
        /// <param name="resources">Gameplay resources to track for session cleanup.</param>
        private void TrackSessionResources(IEnumerable<string> resources)
        {
            if (resources == null)
            {
                return;
            }

            foreach (string resourceName in resources)
            {
                if (!string.IsNullOrWhiteSpace(resourceName))
                {
                    _ = sessionResources.Add(resourceName);
                }
            }
        }

        /// <summary>
        /// Clears session-scoped loading state before starting a fresh gameplay resource session.
        /// </summary>
        private void ResetGameplayResourceSession()
        {
            StopGameplayPrefetch();
            sessionResources.Clear();
            boxResourceScanTask = null;
            boxResourceScanPack = -1;
        }

        /// <summary>
        /// Starts the asynchronous scan that discovers the union of resources used across the current box.
        /// </summary>
        private void StartBoxResourceScanIfNeeded()
        {
            if (Pack < 0)
            {
                return;
            }

            if (boxResourceScanTask != null && boxResourceScanPack == Pack && !boxResourceScanTask.IsFaulted && !boxResourceScanTask.IsCanceled)
            {
                return;
            }

            boxResourceScanPack = Pack;
            boxResourceScanTask = Task.Run(() => LevelResourceScanner.GetBoxResources(Pack));
        }

        /// <summary>
        /// Starts gameplay prefetch immediately if the box scan is done, or polls until scan results are ready.
        /// </summary>
        private void QueueOrPollBoxPrefetch()
        {
            if (GetChild(CHILD_GAME) == null)
            {
                return;
            }

            if (boxResourceScanTask == null)
            {
                return;
            }

            if (boxResourceScanTask.IsCompletedSuccessfully)
            {
                StopBoxScanPollTimer();
                QueueRemainingBoxResourcesForPrefetch(boxResourceScanTask.Result);
                return;
            }

            if (boxScanPollTimer < 0)
            {
                boxScanPollTimer = TimerManager.Schedule(static obj => ((RootController)obj).PollBoxResourceScan(), this, 0.25f);
            }
        }

        /// <summary>
        /// Polls the asynchronous box scan task and queues prefetch work once it completes successfully.
        /// </summary>
        private void PollBoxResourceScan()
        {
            if (boxResourceScanTask == null)
            {
                StopBoxScanPollTimer();
                return;
            }

            if (!boxResourceScanTask.IsCompleted)
            {
                return;
            }

            StopBoxScanPollTimer();
            if (boxResourceScanTask.IsCompletedSuccessfully)
            {
                QueueRemainingBoxResourcesForPrefetch(boxResourceScanTask.Result);
            }
        }

        /// <summary>
        /// Queues the subset of whole-box resources that were not already loaded for the current session.
        /// </summary>
        /// <param name="boxResources">The full resource union discovered for the current box.</param>
        private void QueueRemainingBoxResourcesForPrefetch(HashSet<string> boxResources)
        {
            if (boxResources == null || boxResources.Count == 0)
            {
                return;
            }

            HashSet<string> remainingResources = [.. boxResources];
            remainingResources.ExceptWith(sessionResources);
            if (remainingResources.Count == 0)
            {
                return;
            }

            ResourceMgr resourceMgr = Application.SharedResourceMgr();
            resourceMgr.QueuePrefetchPack(remainingResources);

            if (prefetchDrainTimer < 0)
            {
                prefetchDrainTimer = TimerManager.Schedule(static obj => ((RootController)obj).DrainPrefetchQueue(), this, 1f / 60f);
            }
        }

        /// <summary>
        /// Advances background gameplay prefetch by at most one queued resource.
        /// </summary>
        private void DrainPrefetchQueue()
        {
            ResourceMgr resourceMgr = Application.SharedResourceMgr();
            if (resourceMgr.PrefetchNextResource(out string loadedName))
            {
                if (!string.IsNullOrWhiteSpace(loadedName))
                {
                    _ = sessionResources.Add(loadedName);
                }
            }
            else if (!resourceMgr.HasPendingPrefetchResources())
            {
                StopPrefetchDrainTimer();
            }
        }

        /// <summary>
        /// Stops all gameplay-prefetch timers and clears any queued prefetch work.
        /// </summary>
        private void StopGameplayPrefetch()
        {
            StopBoxScanPollTimer();
            StopPrefetchDrainTimer();
            Application.SharedResourceMgr().ClearPrefetchQueue();
        }

        /// <summary>
        /// Stops the timer that waits for whole-box scan completion.
        /// </summary>
        private void StopBoxScanPollTimer()
        {
            if (boxScanPollTimer >= 0)
            {
                TimerManager.StopTimer(boxScanPollTimer);
                boxScanPollTimer = -1;
            }
        }

        /// <summary>
        /// Stops the timer that drains the silent gameplay-prefetch queue.
        /// </summary>
        private void StopPrefetchDrainTimer()
        {
            if (prefetchDrainTimer >= 0)
            {
                TimerManager.StopTimer(prefetchDrainTimer);
                prefetchDrainTimer = -1;
            }
        }

        /// <summary>Exit code: proceed to the next game level.</summary>
        public const int NEXT_GAME = 0;

        /// <summary>Exit code: return to the main menu.</summary>
        public const int NEXT_MENU = 1;

        /// <summary>Exit code: return to the level picker.</summary>
        public const int NEXT_PICKER = 2;

        /// <summary>Exit code: return to the level picker and advance to the next pack.</summary>
        public const int NEXT_PICKER_NEXT_PACK = 3;

        /// <summary>Exit code: return to the level picker and show the unlock animation.</summary>
        public const int NEXT_PICKER_SHOW_UNLOCK = 4;

        /// <summary>Child slot index for the startup controller.</summary>
        public const int CHILD_START = 0;

        /// <summary>Child slot index for the menu controller.</summary>
        public const int CHILD_MENU = 1;

        /// <summary>Child slot index for the loading controller.</summary>
        public const int CHILD_LOADING = 2;

        /// <summary>Child slot index for the game controller.</summary>
        public const int CHILD_GAME = 3;

        /// <summary>Whether the level picker is active.</summary>
        private bool picker;

        /// <summary>Whether survival mode is active.</summary>
        private bool survival;

        /// <summary>Whether the Crystal overlay is currently shown.</summary>
        private bool inCrystal;

        /// <summary>Whether the Om Nom greeting should play on the next level start.</summary>
        private bool showGreeting;

        /// <summary>Resource pack loaded during the startup splash screen.</summary>
        private static readonly string[] PackStartup = [
            Resources.Img.ZeptoLabLogoLoading,
            Resources.Img.ZeptoLabLogoAnim,
            null
        ];

        /// <summary>
        /// Main menu image resources, terminated by <see langword="null"/>. Loaded at startup and
        /// on every return to the menu, and freed when gameplay replaces the menu.
        /// </summary>
        /// <remarks>
        /// Every image the menu controller's constructor reaches for belongs here. Anything left
        /// out is still decoded and uploaded — just in the single frame that builds the menu,
        /// after the progress bar has already reported completion, which reads as a freeze on
        /// slower texture paths like the browser's. It also never gets freed, so it stays in
        /// memory through gameplay.
        /// </remarks>
        internal static readonly string[] PackMenu =
        [
            Resources.Img.MenuBgr,
            Resources.Img.MenuPopup,
            Resources.Img.MenuLogo,
            Resources.Img.MenuLogoNew,
            Resources.Img.CutTheRopeDXLogo,
            Resources.Img.MenuLevelUi,
            Resources.Img.MenuPackSelection,
            Resources.Img.MenuPackSelection2,
            Resources.Img.MenuPackUI,
            Resources.Img.MenuExtraButtons,
            Resources.Img.MenuBgrShadow,
            Resources.Img.MenuBgrXmas,
            Resources.BackgroundImg.SkinBackground,
            Resources.Img.SkinSelection,
            Resources.Img.CandySelectionFx,
            null
        ];

        /// <summary>Resource pack loaded for gameplay (HUD, candy, spider, etc.).</summary>
        private static readonly string[] PackGame = [
            Resources.Img.MenuButtons,
            Resources.Img.HudUi,
            CandySkinHelper.GetCandyResource(Preferences.GetIntForKey("PREFS_SELECTED_CANDY")),
            Resources.Img.ObjCandyFx,
            Resources.Img.ObjSpider,
            Resources.Img.ConfettiParticles,
            Resources.Img.MenuPause,
            Resources.Img.MenuResults,
            Resources.Fnt.FontNumbersBig,
            null
        ];

        /// <summary>
        /// Builds the menu and loading controllers ahead of the handover from the startup screen,
        /// so their cost lands while the startup progress bar is still on screen and reporting.
        /// Safe to call more than once; the second call is a no-op.
        /// </summary>
        /// <remarks>
        /// Both are constructed only, never activated. <see cref="MenuController.Activate"/> reads
        /// the pack and view to show when it runs, so nothing here depends on state that the
        /// handover sets afterwards.
        /// </remarks>
        public void PrebuildMenuControllers()
        {
            prebuiltLoading ??= new LoadingController(this);
            prebuiltMenu ??= new MenuController(this);
        }

        /// <summary>Loading controller built ahead of the startup handover, or <see langword="null"/>.</summary>
        private LoadingController prebuiltLoading;

        /// <summary>Menu controller built ahead of the startup handover, or <see langword="null"/>.</summary>
        private MenuController prebuiltMenu;

        /// <summary>Set of resource names loaded during the current gameplay session, freed on exit.</summary>
        private readonly HashSet<string> sessionResources = [];

        /// <summary>Async task scanning all levels in the current box for their resource union.</summary>
        private Task<HashSet<string>> boxResourceScanTask;

        /// <summary>Pack index that <see cref="boxResourceScanTask"/> is scanning, or −1.</summary>
        private int boxResourceScanPack = -1;

        /// <summary>Timer handle polling <see cref="boxResourceScanTask"/> completion, or −1 if inactive.</summary>
        private int boxScanPollTimer = -1;

        /// <summary>Timer handle draining the background prefetch queue, or −1 if inactive.</summary>
        private int prefetchDrainTimer = -1;
    }

    /// <summary>Log messages for the root controller's lifecycle.</summary>
    internal static partial class RootControllerLog
    {
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Built the {Controller} controller behind the loading screen in {ElapsedMs:F1} ms")]
        public static partial void ControllerBuilt(ILogger logger, string controller, double elapsedMs);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Loading finished; showing the menu.")]
        public static partial void ShowingMenu(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Loading level pack {Pack} level {Level} '{MapName}': {ResourceCount} resources")]
        public static partial void LevelLoadStarted(
            ILogger logger, int pack, int level, string mapName, int resourceCount);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Level pack {Pack} level {Level} '{MapName}' ready in {ElapsedMs:F1} ms; {Memory}")]
        public static partial void LevelReady(
            ILogger logger, int pack, int level, string mapName, double elapsedMs, string memory);
    }
}
