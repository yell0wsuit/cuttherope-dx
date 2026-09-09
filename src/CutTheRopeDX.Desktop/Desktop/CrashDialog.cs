using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using SDL3;

namespace CutTheRopeDX.Desktop
{
    /// <summary>
    /// The window a player sees when the game stops on its own.
    /// </summary>
    /// <remarks>
    /// A crash that closes the window and leaves nothing behind reads as the game vanishing. This
    /// says what happened and offers the log, which is the one thing that makes the failure worth
    /// reporting rather than just annoying.
    /// </remarks>
    internal static class CrashDialog
    {
        /// <summary>Identifier of the button that only dismisses.</summary>
        private const int OkButton = 0;

        /// <summary>Identifier of the button that reveals the log directory.</summary>
        private const int OpenLogButton = 1;

        /// <summary>
        /// Gets or sets whether a failure may block on a dialog.
        /// </summary>
        /// <remarks>
        /// Off unless a person is watching. A scripted run - a frame-limited smoke test, or CI -
        /// has nobody to press the button, and a modal window there does not report the crash, it
        /// hangs the job until something times out.
        /// </remarks>
        public static bool Enabled { get; set; }

        /// <summary>
        /// Shows the failure and, if asked, opens the directory holding the log.
        /// </summary>
        /// <param name="title">Window title.</param>
        /// <param name="message">What happened, in the player's terms.</param>
        /// <param name="logDirectory">Directory holding this run's log, or null when there is none.</param>
        public static void Show(string title, string message, string logDirectory)
        {
            if (!Enabled)
            {
                return;
            }

            bool offerLog = !string.IsNullOrEmpty(logDirectory);
            if (Prompt(title, message, offerLog) == OpenLogButton && offerLog)
            {
                OpenFolder(logDirectory);
            }
        }

        /// <summary>
        /// Puts the message box up and waits for an answer.
        /// </summary>
        /// <param name="title">Window title.</param>
        /// <param name="message">Body text.</param>
        /// <param name="offerLog">Whether to include the button that reveals the log.</param>
        /// <returns>The identifier of the button pressed, or <see cref="OkButton"/> if none was.</returns>
        /// <remarks>
        /// The buttons are marshalled by hand. SDL wants an array of its own layout behind a
        /// pointer, and the managed struct the binding exposes cannot be laid out into one without
        /// the reflection-based marshaller, which a NativeAOT publish will not carry.
        /// </remarks>
        private static unsafe int Prompt(string title, string message, bool offerLog)
        {
            int count = offerLog ? 2 : 1;
            NativeButton* buttons = (NativeButton*)NativeMemory.Alloc((nuint)(count * sizeof(NativeButton)));
            nint okText = Marshal.StringToCoTaskMemUTF8("OK");
            nint logText = offerLog ? Marshal.StringToCoTaskMemUTF8("Open log location") : 0;

            try
            {
                // Listed before OK so the platform puts the plain dismissal in the position a
                // player expects to confirm with.
                if (offerLog)
                {
                    buttons[0] = new NativeButton { Flags = 0, ButtonId = OpenLogButton, Text = logText };
                }

                buttons[count - 1] = new NativeButton
                {
                    // Enter and Escape both dismiss: neither should reveal a folder by accident.
                    Flags = (uint)(SDL.MessageBoxButtonFlags.ReturnkeyDefault
                        | SDL.MessageBoxButtonFlags.EscapekeyDefault),
                    ButtonId = OkButton,
                    Text = okText,
                };

                SDL.MessageBoxData data = new()
                {
                    Flags = SDL.MessageBoxFlags.Error,
                    Window = 0,
                    Title = title,
                    Message = message,
                    NumButtons = count,
                    Buttons = (nint)buttons,
                    ColorScheme = 0,
                };

                // A box that could not be shown leaves the identifier untouched, so the answer is
                // only trusted when SDL says it asked.
                return SDL.ShowMessageBox(in data, out int pressed) ? pressed : OkButton;
            }
            finally
            {
                Marshal.FreeCoTaskMem(okText);
                if (logText != 0)
                {
                    Marshal.FreeCoTaskMem(logText);
                }

                NativeMemory.Free(buttons);
            }
        }

        /// <summary>
        /// Reveals a directory in the platform's file browser.
        /// </summary>
        /// <param name="directory">The directory to open.</param>
        /// <remarks>
        /// Best effort by design. This runs after the game has already failed, and a desktop with
        /// no file browser wired up is not a reason to fail a second time on the way out.
        /// </remarks>
        private static void OpenFolder(string directory)
        {
            try
            {
                string command = OperatingSystem.IsWindows() ? "explorer"
                    : OperatingSystem.IsMacOS() ? "open"
                    : "xdg-open";
                using Process opened = Process.Start(new ProcessStartInfo(command, directory));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// SDL's own button layout: flags, identifier, and a pointer to UTF-8 text.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeButton
        {
            public uint Flags;
            public int ButtonId;
            public nint Text;
        }
    }
}
