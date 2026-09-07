namespace CutTheRopeDX.Content.Commands
{
    /// <summary>
    /// Identifies an operation supported by the content-builder executable.
    /// </summary>
    public enum ContentCommand
    {
        /// <summary>
        /// Works out which source assets ship and writes the build's own outputs.
        /// </summary>
        Build,

        /// <summary>
        /// Restores missing source assets.
        /// </summary>
        Fetch,

        /// <summary>
        /// Verifies local source assets against the manifest.
        /// </summary>
        Verify,
    }

    /// <summary>
    /// Contains parsed content-builder command-line arguments.
    /// </summary>
    /// <param name="Command">The selected command.</param>
    /// <param name="SourceDirectory">The content source tree, defaulting to <c>content</c>.</param>
    /// <param name="OutputDirectory">Where a build writes its own outputs; required by <see cref="ContentCommand.Build"/>.</param>
    public sealed record ContentCommandLine(
        ContentCommand Command,
        string? SourceDirectory,
        string? OutputDirectory)
    {
        /// <summary>
        /// Parses content-builder command-line arguments.
        /// </summary>
        /// <param name="arguments">Arguments supplied to the executable.</param>
        /// <returns>The parsed command line.</returns>
        public static ContentCommandLine Parse(IReadOnlyList<string> arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            if (arguments.Count == 0)
            {
                return new ContentCommandLine(ContentCommand.Build, null, null);
            }

            ContentCommand command = arguments[0] switch
            {
                "build" => ContentCommand.Build,
                "fetch" => ContentCommand.Fetch,
                "verify" => ContentCommand.Verify,
                _ => throw new ArgumentException($"Unknown command '{arguments[0]}'."),
            };

            string? sourceDirectory = null;
            string? outputDirectory = null;
            for (int index = 1; index < arguments.Count; index++)
            {
                switch (arguments[index])
                {
                    case "--source" or "-s":
                        sourceDirectory = Next(arguments, ref index, "source");
                        break;
                    case "--output" or "-o" when command == ContentCommand.Build:
                        outputDirectory = Next(arguments, ref index, "output");
                        break;
                    default:
                        throw new ArgumentException(
                            $"Unknown {command.ToString().ToLowerInvariant()} option '{arguments[index]}'.");
                }
            }

            return new ContentCommandLine(command, sourceDirectory, outputDirectory);
        }

        /// <summary>Reads the value that follows an option.</summary>
        /// <param name="arguments">The whole command line.</param>
        /// <param name="index">Index of the option; advanced past its value.</param>
        /// <param name="name">Option name, for the failure message.</param>
        private static string Next(IReadOnlyList<string> arguments, ref int index, string name)
        {
            return ++index < arguments.Count
                ? arguments[index]
                : throw new ArgumentException($"The {name} option requires a directory.");
        }
    }
}
