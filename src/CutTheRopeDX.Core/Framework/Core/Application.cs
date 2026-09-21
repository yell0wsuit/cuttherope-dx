using System;

using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.Helpers;

namespace CutTheRopeDX.Framework.Core
{
    /// <summary>
    /// Application bootstrap and shared-service access point for core framework systems.
    /// </summary>
    internal class Application : FrameworkTypes
    {
        /// <summary>
        /// Returns the shared preferences instance.
        /// </summary>
        /// <returns>Shared preferences instance.</returns>
        public static Preferences SharedPreferences()
        {
            return prefs;
        }

        /// <summary>
        /// Returns the shared resource manager instance.
        /// </summary>
        /// <returns>Shared resource manager.</returns>
        public static ResourceMgr SharedResourceMgr()
        {
            return resourceMgr;
        }

        /// <summary>
        /// Returns the shared root controller, creating it on first access.
        /// </summary>
        /// <returns>Shared root controller.</returns>
        public static RootController SharedRootController()
        {
            root ??= RootController.CreateGameRoot();
            return root;
        }

        /// <summary>
        /// Returns the shared root controller only when application startup has already created it.
        /// </summary>
        /// <returns>The existing shared root controller, or <see langword="null"/> before launch.</returns>
        public static RootController ExistingRootController()
        {
            return root;
        }

        /// <summary>
        /// Returns the shared application-settings instance.
        /// </summary>
        /// <returns>Shared application settings.</returns>
        public static ApplicationSettings SharedAppSettings()
        {
            return appSettings;
        }

        /// <summary>
        /// Returns the shared rendering canvas.
        /// </summary>
        /// <returns>Shared canvas instance.</returns>
        public static GLCanvas SharedCanvas()
        {
            return _canvas;
        }

        /// <summary>
        /// Returns the shared sound manager, creating it on first access.
        /// </summary>
        /// <returns>Shared sound manager.</returns>
        public static SoundMgr SharedSoundMgr()
        {
            soundMgr ??= new SoundMgr();
            return soundMgr;
        }

        /// <summary>
        /// Returns the shared movie manager, creating it on first access.
        /// </summary>
        /// <returns>Shared movie manager.</returns>
        public static MovieMgr SharedMovieMgr()
        {
            movieMgr ??= new MovieMgr();
            return movieMgr;
        }

        /// <summary>
        /// Creates the application-settings instance used at startup.
        /// </summary>
        /// <returns>New application-settings instance.</returns>
        public virtual ApplicationSettings CreateAppSettings()
        {
            return new ApplicationSettings();
        }

        /// <summary>
        /// Creates the resource manager used by the application.
        /// </summary>
        /// <returns>New resource manager instance.</returns>
        public virtual ResourceMgr CreateResourceMgr()
        {
            return new ResourceMgr();
        }

        /// <summary>
        /// Creates the shared sound manager instance.
        /// </summary>
        /// <returns>New sound manager instance.</returns>
        public static SoundMgr CreateSoundMgr()
        {
            return new SoundMgr();
        }

        /// <summary>
        /// Creates the preferences store used by the application.
        /// </summary>
        /// <returns>New preferences instance.</returns>
        public virtual Preferences CreatePreferences()
        {
            return new Preferences();
        }

        /// <summary>
        /// Creates the root controller that will own the active controller stack.
        /// </summary>
        /// <returns>New root controller instance.</returns>
        public virtual RootController CreateRootController()
        {
            return RootController.CreateGameRoot();
        }

        /// <summary>
        /// Performs application startup by creating shared services, loading preferences,
        /// configuring orientation, and activating the root controller.
        /// </summary>
        public virtual void ApplicationDidFinishLaunching()
        {
            appSettings = CreateAppSettings();
            prefs = CreatePreferences();
            if (ApplicationSettings.GetBool(7))
            {
                string locale = Preferences.GetStringForKey("PREFS_LOCALE");
                if (string.IsNullOrEmpty(locale))
                {
                    locale = LanguageHelper.ToCode(LanguageHelper.FromSystemCulture());
                }
                appSettings.SetString(8, locale);
            }
            IS_IPAD = false;
            IS_RETINA = false;
            root = CreateRootController();
            soundMgr = CreateSoundMgr();
            movieMgr = CreateMovieMgr();
            _canvas.touchDelegate = root;
            root.Activate();
        }

        /// <summary>
        /// Saves preferences and suspends the root controller when the application loses focus.
        /// </summary>
        public static void ApplicationWillResignActive()
        {
            Preferences.RequestSave();
            if (root != null && !root.IsSuspended())
            {
                root.Suspend();
            }
        }

        /// <summary>
        /// Resumes the root controller when the application becomes active again.
        /// </summary>
        public static void ApplicationDidBecomeActive()
        {
            if (root != null && root.IsSuspended())
            {
                root.Resume();
            }
        }

        /// <summary>
        /// Creates the movie manager used by the application.
        /// </summary>
        /// <returns>New movie manager instance.</returns>
        public virtual MovieMgr CreateMovieMgr()
        {
            return new MovieMgr();
        }

        /// <summary>
        /// Gets a font by its resource name.
        /// </summary>
        /// <param name="fontResourceName">Logical font resource name.</param>
        /// <returns>Loaded font resource, or <see langword="null" /> if loading failed.</returns>
        internal static FontGeneric GetFont(string fontResourceName)
        {
            object resource = SharedResourceMgr().LoadResource(fontResourceName, ResourceMgr.ResourceType.FONT);
            return resource as FontGeneric;
        }

        /// <summary>
        /// Gets a texture by its resource name.
        /// </summary>
        /// <param name="textureResourceName">Logical texture resource name.</param>
        /// <returns>Loaded texture resource.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="textureResourceName"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the texture could not be loaded.</exception>
        internal static Texture2D GetTexture(string textureResourceName)
        {
            if (string.IsNullOrEmpty(textureResourceName))
            {
                throw new ArgumentException("Texture resource name cannot be null or empty.", nameof(textureResourceName));
            }

            object resource = SharedResourceMgr().LoadResource(textureResourceName, ResourceMgr.ResourceType.IMAGE);

            if (resource is Texture2D texture)
            {
                return texture;
            }

            string localizedName = ResourceMgr.HandleLocalizedResource(textureResourceName);
            string resolvedName = string.Equals(textureResourceName, localizedName, StringComparison.Ordinal)
                ? textureResourceName
                : string.IsNullOrEmpty(localizedName)
                    ? textureResourceName
                    : $"{textureResourceName} (localized: {localizedName})";

            throw new InvalidOperationException(
                $"Texture '{resolvedName}' could not be loaded. Ensure the resource name is correct and the JSON+PNG pair exists in content/images/.");
        }

        /// <summary>
        /// Gets a localized string by its key.
        /// </summary>
        /// <param name="stringKey">Localization key to resolve.</param>
        /// <param name="forceEnglish"><see langword="true" /> to always use English; otherwise uses the current language.</param>
        /// <returns>Localized string, or an empty string when the key is <see langword="null"/> or empty.</returns>
        internal static string GetString(string stringKey, bool forceEnglish = false)
        {
            if (string.IsNullOrEmpty(stringKey))
            {
                return string.Empty;
            }

            string languageCode = forceEnglish ? "en" : LanguageHelper.CurrentCode;
            return LocalizationManager.GetString(stringKey, languageCode);
        }

        /// <summary>
        /// Shared preferences instance.
        /// </summary>
        private static Preferences prefs;

        /// <summary>
        /// Shared resource manager instance.
        /// </summary>
        private static readonly ResourceMgr resourceMgr = new();

        /// <summary>
        /// Shared root controller instance.
        /// </summary>
        protected static RootController root;

        /// <summary>
        /// Shared application-settings instance.
        /// </summary>
        private static ApplicationSettings appSettings;

        /// <summary>
        /// Shared rendering canvas instance.
        /// </summary>
        private static readonly GLCanvas _canvas = new();

        /// <summary>
        /// Shared sound manager instance.
        /// </summary>
        private static SoundMgr soundMgr;

        /// <summary>
        /// Shared movie manager instance.
        /// </summary>
        private static MovieMgr movieMgr;
    }
}
