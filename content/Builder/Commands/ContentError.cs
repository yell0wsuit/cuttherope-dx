namespace CutTheRopeDX.Content.Commands
{
    /// <summary>
    /// Writes a content-builder failure in the form MSBuild recognizes as an error.
    /// </summary>
    /// <remarks>
    /// The builder runs inside the game's build through Exec, and the default terminal logger
    /// hides a command's plain output. A line shaped <c>origin : error CODE: message</c> is
    /// promoted to a build error instead, so the reason a build failed is what the person
    /// building sees, in the terminal, an IDE error list and CI annotations alike.
    /// </remarks>
    public static class ContentError
    {
        /// <summary>
        /// The command line was not understood.
        /// </summary>
        public const string InvalidArguments = "CTRDX001";

        /// <summary>
        /// The asset download could not be completed.
        /// </summary>
        public const string DownloadFailed = "CTRDX002";

        /// <summary>
        /// Assets on disk or in the download do not match the manifest.
        /// </summary>
        public const string AssetsMismatched = "CTRDX003";

        /// <summary>
        /// The content source tree could not be read or is incomplete.
        /// </summary>
        public const string ContentUnreadable = "CTRDX004";

        /// <summary>
        /// The builder failed in a way it has no specific report for.
        /// </summary>
        public const string Unexpected = "CTRDX005";

        private const string Origin = "CutTheRopeDX.Content";

        /// <summary>
        /// Writes one error line.
        /// </summary>
        /// <param name="writer">Where the line goes, normally standard error.</param>
        /// <param name="code">One of the codes on this class.</param>
        /// <param name="message">What went wrong; line breaks are folded so it stays one error.</param>
        public static void Write(TextWriter writer, string code, string message)
        {
            string singleLine = string.Join(
                ' ',
                message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
            writer.WriteLine($"{Origin} : error {code}: {singleLine}");
        }
    }
}
