using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>Covers what the easter egg emits, and when it emits nothing at all.</summary>
    public sealed class EasterEggOmNomDrawTests
    {
        private static readonly CTRRectangle Screen = new(-100f, 0f, 2760f, 1440f);

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

        private static EasterEggOmNom AdvancedEgg(int frames)
        {
            EasterEggOmNom egg = new();
            egg.Trigger();
            for (int i = 0; i < frames; i++)
            {
                egg.Update(0.016f);
            }
            return egg;
        }

        [Fact]
        public void DrawsNothingWhenIdle()
        {
            WithRecorder(renderer =>
            {
                EasterEggOmNom egg = new();

                egg.Draw(Screen);

                Assert.Empty(renderer.CapturedLists);
            });
        }

        [Fact]
        public void DrawsOnlyTheDimDuringTheOpeningFade()
        {
            WithRecorder(renderer =>
            {
                EasterEggOmNom egg = AdvancedEgg(6);

                egg.Draw(Screen);

                VertexPositionColor[] dim = Assert.Single(renderer.CapturedLists);
                Assert.Equal(6, dim.Length);
                foreach (VertexPositionColor vertex in dim)
                {
                    Assert.Equal(0, vertex.Color.R);
                    Assert.InRange(vertex.Color.A, (byte)1, (byte)152);
                }
            });
        }

        [Fact]
        public void TheDimCoversTheWholeScreen()
        {
            WithRecorder(renderer =>
            {
                EasterEggOmNom egg = AdvancedEgg(40);

                egg.Draw(Screen);

                float minX = float.MaxValue;
                float maxX = float.MinValue;
                float maxY = float.MinValue;
                foreach (VertexPositionColor vertex in renderer.CapturedLists[0])
                {
                    minX = System.MathF.Min(minX, vertex.Position.X);
                    maxX = System.MathF.Max(maxX, vertex.Position.X);
                    maxY = System.MathF.Max(maxY, vertex.Position.Y);
                }
                Assert.Equal(-100f, minX);
                Assert.Equal(2660f, maxX);
                Assert.Equal(1440f, maxY);
                // Sixty percent black once fully up.
                Assert.Equal(153, renderer.CapturedLists[0][0].Color.A);
            });
        }

        [Fact]
        public void DrawsTheDimThenTwelveLayerBatchesOnceVisible()
        {
            WithRecorder(renderer =>
            {
                EasterEggOmNom egg = AdvancedEgg(40);

                egg.Draw(Screen);

                // The dim, then six layers, each a fill followed by its own fringe.
                Assert.Equal(13, renderer.CapturedLists.Count);
            });
        }

        [Fact]
        public void FadesEveryVertexPremultiplied()
        {
            WithRecorder(renderer =>
            {
                // Past the animation, into the closing fade.
                EasterEggOmNom egg = AdvancedEgg(245);

                egg.Draw(Screen);

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
                        if (vertex.Color.A is > 0 and < 255)
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
                EasterEggOmNom egg = AdvancedEgg(300);

                egg.Draw(Screen);

                Assert.False(egg.IsActive);
                Assert.Empty(renderer.CapturedLists);
            });
        }

        [Fact]
        public void StopsDrawingOnceADismissalHasFaded()
        {
            WithRecorder(renderer =>
            {
                EasterEggOmNom egg = AdvancedEgg(80);

                Assert.True(egg.Cancel());
                for (int i = 0; i < 14; i++)
                {
                    egg.Update(0.016f);
                }
                egg.Draw(Screen);

                Assert.False(egg.IsActive);
                Assert.Empty(renderer.CapturedLists);
            });
        }

        [Fact]
        public void ReusesTheFringeWhileNeitherSizeNorFadeMoves()
        {
            WithRecorder(renderer =>
            {
                // Into the hold, where the scale sits at 6.0 and the fade is complete.
                EasterEggOmNom egg = AdvancedEgg(120);

                egg.Draw(Screen);
                VertexPositionColor[] first = renderer.CapturedLists[2];
                renderer.ClearCaptured();

                egg.Update(0.016f);
                egg.Draw(Screen);

                Assert.Equal(first.Length, renderer.CapturedLists[2].Length);
                Assert.Equal(first[0].Position.X, renderer.CapturedLists[2][0].Position.X);
            });
        }
    }
}
