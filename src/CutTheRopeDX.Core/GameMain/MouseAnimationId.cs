namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Identifiers for the mouse animation timelines on the shared body animation,
    /// matching the iOS BoxGap timeline indices.
    /// </summary>
    internal enum MouseAnimationId
    {
        /// <summary>Entry animation without candy.</summary>
        EntryEmpty = 0,

        /// <summary>Entry animation while carrying candy.</summary>
        EntryWithCandy = 1,

        /// <summary>Idle animation without candy.</summary>
        IdleEmpty = 2,

        /// <summary>Idle animation while carrying candy.</summary>
        Idle = 3,

        /// <summary>Exit animation without candy.</summary>
        ExitEmpty = 4,

        /// <summary>Exit animation while carrying candy.</summary>
        ExitWithCandy = 5,

        /// <summary>Bounce animation used while a mouse is active.</summary>
        Bounce = 6
    }
}
