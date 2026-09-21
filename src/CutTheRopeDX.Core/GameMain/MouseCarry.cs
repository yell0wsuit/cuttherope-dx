using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Physics;

namespace CutTheRopeDX.GameMain
{
    /// <summary>The complete candy payload owned by one mouse.</summary>
    internal sealed class MouseCarry(ConstraintedPoint point, GameObject candy)
    {
        /// <summary>Gets the carried candy's physics point.</summary>
        public ConstraintedPoint Point { get; } = point;

        /// <summary>Gets the carried candy's visual object.</summary>
        public GameObject Candy { get; } = candy;
    }
}
