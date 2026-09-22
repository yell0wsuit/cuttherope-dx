using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Tuning for Time Travel's flying candy (<c>&lt;candy isDriven="true"&gt;</c>), converted from
    /// the original's own world units with <see cref="BombDefinition.TimeTravelToWorldScale"/>.
    /// </summary>
    internal static class CandyFlightDefinition
    {
        /// <summary>
        /// Half-extent of the square around a flying candy that a bouncer edge must cross to stop it
        /// (theirs: an 80x80 box, so 40).
        /// </summary>
        public const float BouncerProbeHalfExtent = 40f * BombDefinition.TimeTravelToWorldScale;

        /// <summary>
        /// Distance a flying candy is set back from a bouncer edge it ends a frame across (theirs: 40).
        /// </summary>
        public const float BouncerStandOff = 40f * BombDefinition.TimeTravelToWorldScale;

        /// <summary>
        /// How far past its natural length a rope may be pulled before it holds the candy back:
        /// the original allows the segment count times the rest length, plus a tenth.
        /// </summary>
        public const float RopeStretchAllowance = 1.1f;

        /// <summary>Horizontal offset of each broken wing's burst from the candy center (theirs: 30).</summary>
        public const float WingBreakOffsetX = 30f * BombDefinition.TimeTravelToWorldScale;

        /// <summary>Vertical offset of both broken wings' bursts above the candy center (theirs: 30).</summary>
        public const float WingBreakOffsetY = 30f * BombDefinition.TimeTravelToWorldScale;

        /// <summary>Seconds per wing-flap frame.</summary>
        public const float FlapFrameDelay = 0.02f;

        /// <summary>First wing-flap quad in <see cref="Resources.Img.ObjCandyTimeTravel"/>.</summary>
        public const int FirstFlapQuad = 0;

        /// <summary>Last wing-flap quad in <see cref="Resources.Img.ObjCandyTimeTravel"/>.</summary>
        public const int LastFlapQuad = 3;

        /// <summary>The wing root drawn over the flapping wings, in <see cref="Resources.Img.ObjCandyTimeTravel"/>.</summary>
        public const int WingRootQuad = 4;

        /// <summary>First broken-wing debris quad in <see cref="Resources.Img.ObjCandyTimeTravel"/>.</summary>
        public const int FirstDebrisQuad = 5;

        /// <summary>Last broken-wing debris quad in <see cref="Resources.Img.ObjCandyTimeTravel"/>.</summary>
        public const int LastDebrisQuad = 6;
    }

    /// <summary>
    /// A flying candy's hold on the candy it copies. While its wings are intact it is not
    /// simulated on its own: every step it is placed at its leader's position plus
    /// <see cref="Offset"/>, so it repeats every move the leader makes. Its wings break, and it
    /// becomes an ordinary candy, as soon as either candy leaves play.
    /// </summary>
    internal sealed class CandyFlight
    {
        /// <summary>Initializes the flight of a candy that copies <paramref name="leader"/>.</summary>
        /// <param name="leader">The candy whose movements this one repeats.</param>
        /// <param name="offset">Starting position of the flying candy relative to its leader.</param>
        /// <param name="wings">The flapping-wings overlay on the flying candy.</param>
        internal CandyFlight(CandyContext leader, Vector offset, Animation wings)
        {
            Leader = leader;
            Offset = offset;
            Wings = wings;
        }

        /// <summary>Gets the candy whose movements this one repeats.</summary>
        public CandyContext Leader { get; }

        /// <summary>Gets the flapping-wings overlay on the flying candy.</summary>
        public Animation Wings { get; }

        /// <summary>
        /// Gets or sets the flying candy's position relative to its leader. It is re-read from the
        /// two candies whenever something stops the flying candy following, so it picks the chase
        /// back up from wherever it was left rather than jumping.
        /// </summary>
        public Vector Offset { get; set; }

        /// <summary>
        /// Gets or sets whether the flying candy hangs where it is this step, neither following nor
        /// simulated: following would have pulled one of its ropes past its stretch allowance, or
        /// its leader is out of sight in transport. Cleared at the start of every step.
        /// </summary>
        public bool Hovering { get; set; }

        /// <summary>
        /// Gets or sets whether the leader was out of sight in transport last step, so the next
        /// placement beside it is a jump rather than a step of the chase.
        /// </summary>
        public bool AwaitingLeader { get; set; }

        /// <summary>Gets whether the wings are still intact.</summary>
        public bool IsFlying { get; private set; } = true;

        /// <summary>Gets or sets the looping wing-flap sound, or <see langword="null"/> while silent.</summary>
        public ISoundInstance FlapSound { get; set; }

        /// <summary>
        /// Breaks the wings. Afterwards the candy is simulated like any other; the flight state
        /// stays attached only so its visuals can be torn down once.
        /// </summary>
        /// <returns><see langword="true"/> the first time; <see langword="false"/> if the wings were already broken.</returns>
        public bool TryBreak()
        {
            if (!IsFlying)
            {
                return false;
            }

            IsFlying = false;
            Hovering = false;
            return true;
        }
    }
}
