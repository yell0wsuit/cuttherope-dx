using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Physics;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class ConstrainedPointTests
    {
        [Theory]
        [InlineData((int)ConstraintType.DISTANCE)]
        [InlineData((int)ConstraintType.NOT_MORE_THAN)]
        [InlineData((int)ConstraintType.NOT_LESS_THAN)]
        public void CoincidentZeroRestConstraintDoesNotInventASeparationDirection(int typeValue)
        {
            Vector position = new(10f, 20f);
            ConstrainedPoint first = new() { pos = position };
            ConstrainedPoint second = new() { pos = position };
            first.AddConstraintwithRestLengthofType(second, 0f, (ConstraintType)typeValue);

            ConstrainedPoint.SatisfyConstraints(first);

            Assert.Equal(position, first.pos);
            Assert.Equal(position, second.pos);
        }
    }
}
