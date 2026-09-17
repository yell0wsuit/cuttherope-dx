using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests.Interactions
{
    /// <summary>
    /// A cluster of static hands whose claws sit within grab range of each other. A steal hands the
    /// candy on, so it jumps between claws without any of them moving.
    /// </summary>
    public sealed class HandClusterTests
    {
        [Fact]
        public void ClusteredStaticHandsStopPassingTheCandyAround()
        {
            GameScene scene = Scenario.New()
                .Candy(90, 225)
                .OmNom(288, 340)
                .Hand(-39, 196, segmentLength: 142, segmentAngle: 0f)
                .Hand(-62, 255, segmentLength: 128, segmentAngle: 0f)
                .Hand(-122, 226, segmentLength: 241, segmentAngle: 0f)
                .Hand(-55, 209, segmentLength: 169, segmentAngle: 0f)
                .Hand(-60, 190, segmentLength: 147, segmentAngle: 0f)
                .Hand(-44, 231, segmentLength: 90, segmentAngle: 0f)
                .Hand(-18, 243, segmentLength: 127, segmentAngle: 0f)
                .Hand(-61, 257, segmentLength: 160, segmentAngle: 0f)
                .Hand(-104, 263, segmentLength: 186, segmentAngle: 0f)
                .Hand(-25, 245, segmentLength: 80, segmentAngle: 0f)
                .Hand(-19, 204, segmentLength: 90, segmentAngle: 0f)
                .Hand(-33, 216, segmentLength: 90, segmentAngle: 0f)
                .Build();
            CandyContext candy = scene.Candy();

            // Let the first grab and any steal chain across the cluster run out.
            Assert.True(
                Interaction.StepUntil(scene, () => candy.Lifecycle.Attachments.Hand != null),
                "no hand grabbed the candy");
            HeadlessGame.StepFrames(scene, 60);

            MechanicalHand owner = candy.Lifecycle.Attachments.Hand;
            Assert.NotNull(owner);
            for (int frame = 0; frame < 300; frame++)
            {
                HeadlessGame.StepFrames(scene, 1);
                Assert.Same(owner, candy.Lifecycle.Attachments.Hand);
            }
        }
    }
}
