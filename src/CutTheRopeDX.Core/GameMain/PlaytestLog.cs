using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Log messages for a playtest session, shared by both heads.
    /// </summary>
    /// <remarks>
    /// The editor already learns about these over its own channel - a stdout handshake on desktop,
    /// a BroadcastChannel in the browser - and none of that reaches the player's log. These are the
    /// same events written where someone reading a report afterwards can see them.
    /// </remarks>
    internal static partial class PlaytestLog
    {
        /// <summary>Records a session that started against an editor-supplied level.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="levelPath">Where the level was handed over.</param>
        [LoggerMessage(Level = LogLevel.Information, Message = "Playtest active for '{LevelPath}'")]
        public static partial void SessionActive(ILogger logger, string levelPath);

        /// <summary>Records a launch that looked like a playtest but did not become one.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="reason">Why the normal game is starting instead.</param>
        [LoggerMessage(Level = LogLevel.Information, Message = "Playtest inactive: {Reason}")]
        public static partial void SessionInactive(ILogger logger, string reason);

        /// <summary>Records the identifying line the editor waits for.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="line">The handshake exactly as it was written.</param>
        [LoggerMessage(Level = LogLevel.Information, Message = "Playtest handshake: {Line}")]
        public static partial void Handshake(ILogger logger, string line);

        /// <summary>Records a level arriving from the editor.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="characters">Size of the level document.</param>
        [LoggerMessage(Level = LogLevel.Information, Message = "Playtest level received ({Characters} characters)")]
        public static partial void LevelReceived(ILogger logger, int characters);

        /// <summary>Records how an edit to the running level was applied.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="kind">Whether the level restarted in place or reloaded fully.</param>
        /// <param name="resourceCount">How many resources the edited level needs.</param>
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Playtest level changed; {Kind} reload, {ResourceCount} resources required")]
        public static partial void LevelChanged(ILogger logger, CustomLevelReloadKind kind, int resourceCount);

        /// <summary>
        /// Records a level this build would not load.
        /// </summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="reason">The failure, worded as the editor receives it.</param>
        /// <remarks>
        /// The same text also goes to standard error undecorated, which is the only thing the
        /// editor reads. This is the copy for the player's log, not a replacement for that.
        /// </remarks>
        [LoggerMessage(Level = LogLevel.Warning, Message = "Playtest level rejected: {Reason}")]
        public static partial void LevelRejected(ILogger logger, string reason);

        /// <summary>Records the session being closed from the game's side.</summary>
        /// <param name="logger">Destination logger.</param>
        [LoggerMessage(Level = LogLevel.Information, Message = "Playtest session closed.")]
        public static partial void SessionClosed(ILogger logger);
    }
}
