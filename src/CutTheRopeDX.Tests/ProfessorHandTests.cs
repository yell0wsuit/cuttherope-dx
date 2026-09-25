using System.Reflection;

using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Tests.Interactions;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the Experiments professor's hand, which carries the candy in on the first level of
    /// a pack and holds the level still until it lets go.
    /// </summary>
    public sealed class ProfessorHandTests
    {
        [Fact]
        public void HandPausesTheFirstLevelUntilItHasLeft()
        {
            WithStyle(MenuStyle.Experiments, () =>
            {
                GameScene scene = LoadScene(level: 0);
                Assert.True(scene.ProfessorHandHolds);
                Assert.True(scene.ProfessorHandPausesPlay);
                float startY = scene.Candy().WholeBody.Point.pos.Y;

                // Waiting and lowering take 1.2 seconds, then it lets go of the candy.
                Tick(scene, 1.4f);
                Assert.False(scene.ProfessorHandHolds);
                Assert.True(scene.ProfessorHandPausesPlay);

                // Lingering and rising take another 1.1; nothing moves until it has gone.
                Tick(scene, 0.8f);
                Assert.True(scene.ProfessorHandPausesPlay);
                Assert.Equal(startY, scene.Candy().WholeBody.Point.pos.Y);

                Tick(scene, 0.5f);
                Assert.False(scene.ProfessorHandPausesPlay);
            });
        }

        [Fact]
        public void HandOnlyCarriesTheFirstLevelOfAPack()
        {
            WithStyle(MenuStyle.Experiments, () => Assert.False(LoadScene(level: 1).ProfessorHandPausesPlay));
        }

        [Fact]
        public void ClassicMenusHaveNoHand()
        {
            WithStyle(MenuStyle.Classic, () => Assert.False(LoadScene(level: 0).ProfessorHandPausesPlay));
        }

        [Fact]
        public void ArmReachesTheTopOfAViewportTallerThanTheLevel()
        {
            // Booting resets the surface, so the portrait size goes on between boot and load.
            _ = HeadlessGame.Boot();
            LayoutSurfaces.WithSurface(400, 1280, () => WithStyle(MenuStyle.Experiments, () =>
            {
                GameController controller = HeadlessGame.LoadLevelWithController(0, 0);
                GameScene scene = (GameScene)controller.GetView(0).GetChild(0);
                Camera2D camera = Read<Camera2D>(scene, "camera");
                BaseElement hand = Read<BaseElement>(scene, "professorHand");
                TiledImage sleeve = Read<TiledImage>(scene, "professorHandSleeve");
                float visibleTop = camera.RenderPos.Y;
                Assert.True(visibleTop < 0f, "a tall portrait viewport shows world above the level");

                // Waiting: the whole hand is above the view, not hovering inside it.
                Assert.True(hand.y + hand.height <= visibleTop);

                // Resting on the candy: the arm runs all the way up out of the view.
                Tick(scene, 1.3f);
                Assert.True(scene.ProfessorHandPausesPlay);
                Assert.True(hand.y > visibleTop);
                Assert.True(hand.y + sleeve.y <= camera.RenderPos.Y);
            }));
        }

        private static T Read<T>(object target, string field)
        {
            object value = target.GetType()
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
            return Assert.IsAssignableFrom<T>(value);
        }

        private static GameScene LoadScene(int level)
        {
            _ = HeadlessGame.Boot();
            GameController controller = HeadlessGame.LoadLevelWithController(0, level);
            return (GameScene)controller.GetView(0).GetChild(0);
        }

        private static void Tick(GameScene scene, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.016f)
            {
                scene.Update(0.016f);
            }
        }

        private static void WithStyle(MenuStyle style, System.Action body)
        {
            MenuStyle previous = MenuTheme.Current;
            MenuTheme.Current = style;
            try
            {
                body();
            }
            finally
            {
                MenuTheme.Current = previous;
            }
        }
    }
}
