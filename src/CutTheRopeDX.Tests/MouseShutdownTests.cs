using CutTheRopeDX.GameMain;
using CutTheRopeDX.Tests.Interactions;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class MouseShutdownTests
    {
        [Fact]
        public void MouseStillEnteringItsHoleRetreatsWhenTheLevelIsWon()
        {
            // A mouse turns active only once its entry animation ends. A level won inside that
            // window has to send it home too, or it finishes entering and stands in its hole with
            // nothing to carry and no way to leave.
            GameScene scene = Scenario.New()
                .Candy(40, 40)
                .OmNom(20, 460)
                .Mouse(160, 200)
                .Build();
            Mouse mouse = scene.Mice()[0];
            Assert.False(mouse.IsActive);

            scene.GameWon();
            HeadlessGame.StepFrames(scene, 60);

            Assert.False(mouse.IsActive);
        }
    }
}
