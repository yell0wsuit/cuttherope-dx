using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class TimeTravelScreenTests
    {
        /// <summary>The visible bounds a surface gives: at the origin, short side 1440 logical units.</summary>
        private static Rectangle Visible(int width, int height)
        {
            float scale = 1440f / Math.Min(width, height);
            return new Rectangle(0f, 0f, width * scale, height * scale);
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void SceneIsContainedAndTouchesOneAxis(string name, int width, int height)
        {
            _ = name;
            Rectangle visible = Visible(width, height);
            TimeTravelScreen screen = new(visible);

            float sceneW = TimeTravelScreen.SceneWidth * screen.Scale;
            float sceneH = TimeTravelScreen.SceneHeight * screen.Scale;
            Assert.True(sceneW <= visible.w + 0.01f && sceneH <= visible.h + 0.01f);
            Assert.True(MathF.Abs(sceneW - visible.w) < 0.01f || MathF.Abs(sceneH - visible.h) < 0.01f);
            Assert.Equal(visible.w, screen.FullWidth * screen.Scale, 2);
            Assert.Equal(visible.h, screen.FullHeight * screen.Scale, 2);
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void PinsAtTheSceneEdgeLandOnTheVisibleEdge(string name, int width, int height)
        {
            _ = name;
            Rectangle visible = Visible(width, height);
            TimeTravelScreen screen = new(visible);

            Vector topLeft = screen.Pin(0f, 0f, TimeTravelAttach.Left | TimeTravelAttach.Top);
            Vector bottomRight = screen.Pin(
                TimeTravelScreen.SceneWidth, TimeTravelScreen.SceneHeight, TimeTravelAttach.Right | TimeTravelAttach.Bottom);
            Vector topLeftDesign = screen.ToDesign(topLeft.X, topLeft.Y);
            Vector bottomRightDesign = screen.ToDesign(bottomRight.X, bottomRight.Y);

            Assert.Equal(visible.x, topLeftDesign.X, 2);
            Assert.Equal(visible.y, topLeftDesign.Y, 2);
            Assert.Equal(visible.x + visible.w, bottomRightDesign.X, 2);
            Assert.Equal(visible.y + visible.h, bottomRightDesign.Y, 2);
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void ScaleToFullSpansTheVisibleWidth(string name, int width, int height)
        {
            _ = name;
            Rectangle visible = Visible(width, height);
            TimeTravelScreen screen = new(visible);

            Vector full = screen.FullScale(TimeTravelAttach.ScaleToFullX);

            Assert.Equal(visible.w, TimeTravelScreen.SceneWidth * full.X * screen.Scale, 2);
            Assert.Equal(1f, full.Y);
        }

        [Fact]
        public void StretchToFullCoversWithTheBleedTrimmed()
        {
            Rectangle visible = new(0f, 0f, 2560f, 1440f);

            Vector scale = TimeTravelScreen.StretchToFull(126f, 188f, 3f, visible);

            Assert.Equal(2560f, (126f - 6f) * scale.X, 2);
            Assert.Equal(1440f, (188f - 6f) * scale.Y, 2);
        }

        [Fact]
        public void AssetScaleConvertsAtlasPixelsToTheScene()
        {
            TimeTravelScreen screen = new(new Rectangle(0f, 0f, 2560f, 1440f));

            Assert.Equal(1.5f, screen.Scale, 4);
            Assert.Equal(1.5f / 1.56f, screen.AssetScale, 4);
        }
    }
}
