using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Tests.Interactions;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class SteamTubeValveTests
    {
        [Fact]
        public void ValveTakesATapWithinThreeTimesTheWindowsPhoneReach()
        {
            // Windows Phone answers a tap within 40 units of the valve, and DX's world is three
            // times larger, so 100 units out has to reach it.
            (GameScene scene, SteamTube tube) = Rig();
            int state = tube.steamState;

            TapFromValve(scene, tube, 100f);

            Assert.NotEqual(state, tube.steamState);
        }

        [Fact]
        public void ValveIgnoresATapBeyondItsScaledReach()
        {
            (GameScene scene, SteamTube tube) = Rig();
            int state = tube.steamState;

            TapFromValve(scene, tube, 130f);

            Assert.Equal(state, tube.steamState);
        }

        private static (GameScene Scene, SteamTube Tube) Rig()
        {
            GameScene scene = Scenario.New()
                .Candy(40, 40)
                .OmNom(20, 460)
                .SteamTube(160, 300)
                .Build();
            return (scene, scene.SteamTubes()[0]);
        }

        private static void TapFromValve(GameScene scene, SteamTube tube, float distance)
        {
            Vector valve = new(tube.x, tube.y + (28f * tube.GetHeightScale()));
            Vector tap = scene.ScreenPositionOf(new Vector(valve.X + distance, valve.Y));
            _ = scene.TouchDownXYIndex(tap.X, tap.Y, 1);
            _ = scene.TouchUpXYIndex(tap.X, tap.Y, 1);
        }
    }
}
