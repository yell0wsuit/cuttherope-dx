using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>Covers what the easter egg emits, and when it emits nothing at all.</summary>
    public sealed class EasterEggOmNomDrawTests
    {
        private static void WithRecorder(System.Action<RecordingRenderBackend> body)
        {
            RecordingRenderBackend renderer = new();
            PlatformServices.Render = renderer;
            try
            {
                body(renderer);
            }
            finally
            {
                PlatformServices.Render = new ThrowingRenderBackend();
            }
        }

        [Fact]
        public void DrawsNothingWhenIdle()
        {
            WithRecorder(renderer =>
            {
                EasterEggOmNom egg = new();

                egg.Draw();

                Assert.Empty(renderer.CapturedLists);
            });
        }

        [Fact]
        public void DrawsNothingDuringTheOpeningFade()
        {
            WithRecorder(renderer =>
            {
                EasterEggOmNom egg = new();
                egg.Trigger();
                egg.Update(0.016f);

                egg.Draw();

                Assert.Empty(renderer.CapturedLists);
            });
        }

        [Fact]
        public void DrawsTwelveBatchesOnceVisible()
        {
            WithRecorder(renderer =>
            {
                EasterEggOmNom egg = new();
                egg.Trigger();
                for (int i = 0; i < 40; i++)
                {
                    egg.Update(0.016f);
                }

                egg.Draw();

                // Six layers, each a fill followed by its own fringe.
                Assert.Equal(12, renderer.CapturedLists.Count);
            });
        }

        [Fact]
        public void FadesEveryVertexPremultiplied()
        {
            WithRecorder(renderer =>
            {
                EasterEggOmNom egg = new();
                egg.Trigger();
                // Past the animation, into the closing fade.
                for (int i = 0; i < 245; i++)
                {
                    egg.Update(0.016f);
                }

                egg.Draw();

                bool sawPartialAlpha = false;
                foreach (VertexPositionColor[] batch in renderer.CapturedLists)
                {
                    foreach (VertexPositionColor vertex in batch)
                    {
                        Assert.True(
                            vertex.Color.R <= vertex.Color.A
                                && vertex.Color.G <= vertex.Color.A
                                && vertex.Color.B <= vertex.Color.A,
                            "a channel exceeded alpha, so the color is not premultiplied");
                        if (vertex.Color.A > 0 && vertex.Color.A < 255)
                        {
                            sawPartialAlpha = true;
                        }
                    }
                }

                Assert.True(sawPartialAlpha, "expected the closing fade to be under way");
            });
        }

        [Fact]
        public void StopsDrawingAfterTheFadeCompletes()
        {
            WithRecorder(renderer =>
            {
                EasterEggOmNom egg = new();
                egg.Trigger();
                for (int i = 0; i < 300; i++)
                {
                    egg.Update(0.016f);
                }

                egg.Draw();

                Assert.False(egg.IsActive);
                Assert.Empty(renderer.CapturedLists);
            });
        }

        [Fact]
        public void ReusesTheFringeWhileNeitherSizeNorFadeMoves()
        {
            WithRecorder(renderer =>
            {
                EasterEggOmNom egg = new();
                egg.Trigger();
                // Into the hold, where the scale sits at 6.0 and the fade is complete.
                for (int i = 0; i < 120; i++)
                {
                    egg.Update(0.016f);
                }

                egg.Draw();
                VertexPositionColor[] first = renderer.CapturedLists[1];
                renderer.ClearCaptured();

                egg.Update(0.016f);
                egg.Draw();

                // Same band, not merely an equal one: nothing rebuilt it.
                Assert.Equal(first.Length, renderer.CapturedLists[1].Length);
                Assert.Equal(first[0].Position.X, renderer.CapturedLists[1][0].Position.X);
            });
        }

        [Fact]
        public void ReleasesTheFreezeBeforeItStopsDrawing()
        {
            EasterEggOmNom egg = new();
            egg.Trigger();
            for (int i = 0; i < 245; i++)
            {
                egg.Update(0.016f);
            }

            Assert.False(egg.FreezesGameplay);
            Assert.True(egg.IsActive);
        }
    }
}
