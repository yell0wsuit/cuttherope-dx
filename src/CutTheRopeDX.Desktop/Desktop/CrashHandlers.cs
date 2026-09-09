using System;
using System.Threading.Tasks;

using CutTheRopeDX.Framework.Diagnostics;

using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.Desktop
{
    /// <summary>
    /// Writes a failure the game did not catch into the log before the process goes.
    /// </summary>
    /// <remarks>
    /// Without this a crash leaves nothing behind at all on Windows, where the release publishes
    /// as a windowed executable with no console attached to print to.
    /// </remarks>
    internal static partial class CrashHandlers
    {
        private static ILoggerFactory owner;

        /// <summary>
        /// Hooks the two failure paths that would otherwise be silent.
        /// </summary>
        /// <param name="factory">The factory to flush when the process is going down.</param>
        public static void Install(ILoggerFactory factory)
        {
            owner = factory;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
            TaskScheduler.UnobservedTaskException += OnUnobserved;
        }

        /// <summary>
        /// Logs a fatal exception and flushes, because the process aborts as soon as this returns.
        /// </summary>
        /// <param name="sender">Unused.</param>
        /// <param name="args">Carries the exception and whether the process is going down.</param>
        /// <remarks>
        /// Everything here is inside a catch of its own. This runs on a runtime that is already
        /// failing, and an exception raised here would replace the original diagnosis with a
        /// worse one.
        /// </remarks>
        internal static void OnUnhandled(object sender, UnhandledExceptionEventArgs args)
        {
            try
            {
                ILogger logger = Log.For(LogCategories.SdlHost);
                Unhandled(logger, args.ExceptionObject as Exception, args.IsTerminating);

                // The file provider buffers, and nothing else gets to run before the abort.
                owner?.Dispose();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Logs a task failure nobody awaited and marks it observed, keeping it non-fatal.
        /// </summary>
        /// <param name="sender">Unused.</param>
        /// <param name="args">Carries the exception and the observed flag.</param>
        /// <remarks>
        /// A net for failures nobody awaited rather than a fix for a known one. The Discord and
        /// update-check tasks each discard their own exceptions in a catch around the whole body,
        /// so neither reaches here; what can is a stored task whose result is never read.
        /// </remarks>
        internal static void OnUnobserved(object sender, UnobservedTaskExceptionEventArgs args)
        {
            try
            {
                ILogger logger = Log.For(LogCategories.SdlHost);
                Unobserved(logger, args.Exception);
                args.SetObserved();
            }
            catch (Exception)
            {
            }
        }

        [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception, terminating={Terminating}")]
        private static partial void Unhandled(ILogger logger, Exception exception, bool terminating);

        [LoggerMessage(Level = LogLevel.Error, Message = "Unobserved task exception")]
        private static partial void Unobserved(ILogger logger, Exception exception);
    }
}
