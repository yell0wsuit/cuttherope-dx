using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.GameMain;

using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.Browser
{
    /// <summary>Applies the launch options the page URL carries in place of a command line.</summary>
    internal static partial class LaunchOptions
    {
        /// <summary>Imports launch-options.js. Must be awaited once before <see cref="ApplyMenu"/>.</summary>
        /// <returns>A task that completes when the module is available.</returns>
        public static Task ImportAsync()
        {
            return JSHost.ImportAsync("launch-options", "../launch-options.js");
        }

        /// <summary>
        /// Sets the menu style from <c>?menu=</c>. The menu, loading and pause scenes read it as they
        /// are built, so this runs before the game boots. An unknown value keeps the classic menus.
        /// </summary>
        public static void ApplyMenu()
        {
            string value = MenuFromQuery();
            if (value.Length == 0)
            {
                return;
            }
            if (!MenuTheme.TryParse(value, out MenuStyle style))
            {
                LaunchOptionsLog.UnknownMenu(Log.For(LogCategories.Application), value);
            }
            MenuTheme.Current = style;
        }

        [JSImport("menuFromQuery", "launch-options")]
        private static partial string MenuFromQuery();
    }

    /// <summary>Log messages for URL launch options.</summary>
    internal static partial class LaunchOptionsLog
    {
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Unknown ?menu= value '{Value}'; expected 'classic', 'experiments' or 'timetravel'. Using the classic menus.")]
        public static partial void UnknownMenu(ILogger logger, string value);
    }
}
