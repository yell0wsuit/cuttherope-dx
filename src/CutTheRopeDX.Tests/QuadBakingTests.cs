using System;
using System.Numerics;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Baking a sprite vertex on the CPU: the transform and tint the fixed-function pipeline
    /// used to apply, which every renderer now receives already applied.
    /// </summary>
    /// <remarks>
    /// These began as characterization tests over the MonoGame backend's own baking. That backend
    /// is gone, but the arithmetic outlived it: <see cref="QuadBaking"/> is what the Skia renderer
    /// draws through on every host, so the conventions asserted here are still the ones the game
    /// looks correct under.
    /// </remarks>
    public sealed class QuadBakingTests
    {
        private const float Tolerance = 1e-4f;

        [Fact]
        public void PremultipliedTintMatchesTheFixedFunctionDiffuseConvention()
        {
            Color baked = QuadBaking.BakePremultipliedTint(new Color(200, 100, 50, 128));

            Assert.Equal(128, baked.A);
            Assert.Equal((byte)(200 * 128 / 255), baked.R);
            Assert.Equal((byte)(100 * 128 / 255), baked.G);
            Assert.Equal((byte)(50 * 128 / 255), baked.B);
        }

        [Fact]
        public void OpaqueWhiteTintBakesToWhite()
        {
            Color baked = QuadBaking.BakePremultipliedTint(new Color(255, 255, 255, 255));

            Assert.Equal(new Color(255, 255, 255, 255), baked);
        }

        [Fact]
        public void ZeroAlphaIsInvisible()
        {
            Assert.True(QuadBaking.IsInvisible(new Color(255, 255, 255, 0)));
            Assert.False(QuadBaking.IsInvisible(new Color(255, 255, 255, 1)));
        }

        [Fact]
        public void TranslationBakesIntoPosition()
        {
            VertexPositionNormalTexture source = new(
                new Vector3(1f, 2f, 0f), Vector3.UnitZ, new Vector2(0.25f, 0.75f));

            VertexPositionColorTexture baked = QuadBaking.Bake(
                source, Matrix4x4.CreateTranslation(10f, 20f, 0f), White);

            Assert.Equal(11f, baked.Position.X, Tolerance);
            Assert.Equal(22f, baked.Position.Y, Tolerance);
        }

        [Fact]
        public void RotationBakesIntoPosition()
        {
            VertexPositionNormalTexture source = new(
                new Vector3(1f, 0f, 0f), Vector3.UnitZ, Vector2.Zero);

            VertexPositionColorTexture baked = QuadBaking.Bake(
                source, Matrix4x4.CreateRotationZ(MathF.PI / 2f), White);

            Assert.Equal(0f, baked.Position.X, Tolerance);
            Assert.Equal(1f, baked.Position.Y, Tolerance);
        }

        [Fact]
        public void ScaleBakesIntoPosition()
        {
            VertexPositionNormalTexture source = new(
                new Vector3(2f, 3f, 0f), Vector3.UnitZ, Vector2.Zero);

            VertexPositionColorTexture baked = QuadBaking.Bake(
                source, Matrix4x4.CreateScale(2f, 0.5f, 1f), White);

            Assert.Equal(4f, baked.Position.X, Tolerance);
            Assert.Equal(1.5f, baked.Position.Y, Tolerance);
        }

        [Fact]
        public void SkewBakesIntoPosition()
        {
            float cos45 = MathF.Cos(MathF.PI / 4f);
            Matrix4x4 skew = Matrix4x4.Identity;
            skew.M21 = -cos45;
            skew.M22 = cos45;
            VertexPositionNormalTexture source = new(
                new Vector3(0f, 1f, 0f), Vector3.UnitZ, Vector2.Zero);

            VertexPositionColorTexture baked = QuadBaking.Bake(source, skew, White);

            Assert.Equal(-cos45, baked.Position.X, Tolerance);
            Assert.Equal(cos45, baked.Position.Y, Tolerance);
        }

        [Fact]
        public void UvCoordinatesPassThroughUnchanged()
        {
            VertexPositionNormalTexture source = new(
                Vector3.Zero, Vector3.UnitZ, new Vector2(0.25f, 0.75f));

            VertexPositionColorTexture baked = QuadBaking.Bake(
                source, Matrix4x4.CreateScale(3f), White);

            Assert.Equal(new Vector2(0.25f, 0.75f), baked.TextureCoordinate);
        }

        private static Color White => new(255, 255, 255, 255);
    }
}
