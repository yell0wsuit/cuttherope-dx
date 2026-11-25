namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Logical animation states exposed by the target animation controller.
    /// </summary>
    internal enum TargetAnimationState
    {
        /// <summary>Default idle loop.</summary>
        IdleLoop,

        /// <summary>First idle variation.</summary>
        IdleVariationOne,

        /// <summary>Second idle variation.</summary>
        IdleVariationTwo,

        /// <summary>Third idle variation.</summary>
        IdleVariationThree,

        /// <summary>Excited reaction animation.</summary>
        Excited,

        /// <summary>Mouth opening transition.</summary>
        MouthOpening,

        /// <summary>Mouth closing transition.</summary>
        MouthClosing,

        /// <summary>Chewing animation.</summary>
        Chewing,

        /// <summary>Sad reaction animation.</summary>
        Sad,

        /// <summary>Transition from idle loop into sleeping.</summary>
        IdleToSleep,

        /// <summary>Sleeping animation.</summary>
        Sleeping,

        /// <summary>Greeting animation.</summary>
        Greeting,

        /// <summary>Directional greeting where Om Nom turns its head toward the left.</summary>
        GreetLeft,

        /// <summary>Directional greeting where Om Nom turns its head toward the right.</summary>
        GreetRight,

        /// <summary>Directional greeting where Om Nom turns its head upward.</summary>
        GreetUp,

        /// <summary>Directional greeting where Om Nom turns its head downward.</summary>
        GreetDown,

        /// <summary>Om Nom drawn out of the time spiral as a pack begins.</summary>
        LevelIntro,

        /// <summary>Om Nom drawn back into the time spiral as a pack ends.</summary>
        LevelOutro,
    }
}
