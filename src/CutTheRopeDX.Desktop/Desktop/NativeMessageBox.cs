using System;
using System.Runtime.InteropServices;
using System.Text;

using SDL3;

namespace CutTheRopeDX.Desktop
{
    /// <summary>
    /// A blocking platform message box with custom buttons, usable before any SDL window exists.
    /// </summary>
    internal static class NativeMessageBox
    {
        /// <summary>
        /// One button: the identifier returned when it is pressed, its label, and its key defaults.
        /// </summary>
        internal readonly record struct Button(int Id, string Text, SDL.MessageBoxButtonFlags Flags = 0);

        /// <summary>
        /// Puts the message box up and waits for an answer.
        /// </summary>
        /// <param name="flags">Severity of the box.</param>
        /// <param name="title">Window title.</param>
        /// <param name="message">Body text, wrapped here for platforms that do not wrap it.</param>
        /// <param name="fallback">Identifier returned when the box could not be shown.</param>
        /// <param name="buttons">Buttons, in the order the platform is handed them.</param>
        /// <returns>The identifier of the button pressed, or <paramref name="fallback"/> if none was.</returns>
        /// <remarks>
        /// The buttons are marshalled by hand. SDL wants an array of its own layout behind a
        /// pointer, and the managed struct the binding exposes cannot be laid out into one without
        /// the reflection-based marshaller, which a NativeAOT publish will not carry.
        /// </remarks>
        public static unsafe int Show(
            SDL.MessageBoxFlags flags, string title, string message, int fallback, params Button[] buttons)
        {
            NativeButton* native = (NativeButton*)NativeMemory.AllocZeroed((nuint)(buttons.Length * sizeof(NativeButton)));
            try
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    native[i] = new NativeButton
                    {
                        Flags = (uint)buttons[i].Flags,
                        ButtonId = buttons[i].Id,
                        Text = Marshal.StringToCoTaskMemUTF8(buttons[i].Text),
                    };
                }

                SDL.MessageBoxData data = new()
                {
                    Flags = flags,
                    Window = 0,
                    Title = title,
                    Message = WrapMessage(message),
                    NumButtons = buttons.Length,
                    Buttons = (nint)native,
                    ColorScheme = 0,
                };

                // A box that could not be shown leaves the identifier untouched, so the answer is
                // only trusted when SDL says it asked.
                return SDL.ShowMessageBox(in data, out int pressed) ? pressed : fallback;
            }
            finally
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (native[i].Text != 0)
                    {
                        Marshal.FreeCoTaskMem(native[i].Text);
                    }
                }

                NativeMemory.Free(native);
            }
        }

        /// <summary>Adds explicit line breaks for native message boxes that do not wrap text.</summary>
        internal static string WrapMessage(string message)
        {
            const int columns = 80;
            StringBuilder result = new();
            foreach (string paragraph in message.ReplaceLineEndings("\n").Split('\n'))
            {
                string remaining = paragraph;
                while (remaining.Length > columns)
                {
                    int end = remaining.LastIndexOf(' ', columns, columns + 1);
                    bool atSpace = end > 0;
                    if (!atSpace)
                    {
                        end = columns;
                        if (char.IsHighSurrogate(remaining[end - 1]))
                        {
                            end--;
                        }
                    }
                    _ = result.Append(remaining.AsSpan(0, end)).Append('\n');
                    remaining = remaining[(end + (atSpace ? 1 : 0))..];
                }
                _ = result.Append(remaining).Append('\n');
            }
            return result.ToString(0, result.Length - 1);
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
