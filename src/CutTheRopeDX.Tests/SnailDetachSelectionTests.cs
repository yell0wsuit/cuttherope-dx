using CutTheRopeDX.Framework.Physics;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// When a candy leaves play (eaten, broken, transported), only the snails riding THAT candy detach.
    /// A single-candy engine could tear every snail down at once; with several candies alive, detaching
    /// must be keyed to the candy's physics point so an eaten candy does not drop another candy's snail.
    /// </summary>
    public class SnailDetachSelectionTests
    {
        [Fact]
        public void ShouldDetachTrueForAnActiveSnailRidingThisCandy()
        {
            ConstrainedPoint candy = new();
            Assert.True(SnailDetachSelection.ShouldDetach(
                snailActive: true, snailAttachedPoint: candy, targetPoint: candy));
        }

        [Fact]
        public void ShouldDetachFalseForASnailRidingADifferentCandy()
        {
            // The multi-candy isolation invariant: detaching snails from candy A must leave candy B's snail on.
            ConstrainedPoint candyA = new();
            ConstrainedPoint candyB = new();
            Assert.False(SnailDetachSelection.ShouldDetach(
                snailActive: true, snailAttachedPoint: candyB, targetPoint: candyA));
        }

        [Fact]
        public void ShouldDetachFalseForAnInactiveSnail()
        {
            // Only riding (active) snails detach; one that is spawning or already vanishing is left alone.
            ConstrainedPoint candy = new();
            Assert.False(SnailDetachSelection.ShouldDetach(
                snailActive: false, snailAttachedPoint: candy, targetPoint: candy));
        }

        [Fact]
        public void ShouldDetachFalseWhenSnailRidesNothing()
        {
            ConstrainedPoint candy = new();
            Assert.False(SnailDetachSelection.ShouldDetach(
                snailActive: true, snailAttachedPoint: null, targetPoint: candy));
        }
    }
}
