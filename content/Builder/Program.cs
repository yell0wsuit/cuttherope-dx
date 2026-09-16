using CutTheRopeDX.Content.Commands;

try
{
    ContentCommandLine commandLine = ContentCommandLine.Parse(args);
    return await AssetCommands.RunAsync(commandLine);
}
catch (ArgumentException exception)
{
    ContentError.Write(Console.Error, ContentError.InvalidArguments, exception.Message);
    return 1;
}
// Anything else would reach the runtime's own crash report, which the build log hides.
catch (Exception exception)
{
    ContentError.Write(
        Console.Error,
        ContentError.Unexpected,
        $"{exception.GetType().Name}: {exception.Message}");
    Console.Error.WriteLine(exception);
    return 1;
}
