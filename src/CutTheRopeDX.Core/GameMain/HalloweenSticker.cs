using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// A costumed Om Nom stuck next to the Halloween result panel - after the bats on three stars,
    /// straight away otherwise: one of Cut the Rope 3.3.0's five, at a random spot, size and tilt,
    /// fading and zooming in to that size.
    /// </summary>
    internal static class HalloweenSticker
    {
        /// <summary>Number of costumes in <see cref="Resources.Img.MenuResultScreenHalloween"/>.</summary>
        public const int Costumes = 5;

        /// <summary>Smallest tilt, in degrees, so a sticker always reads as stuck on by hand.</summary>
        public const float MinTilt = 4f;

        /// <summary>Largest tilt, in degrees, either way.</summary>
        public const float MaxTilt = 15f;

        /// <summary>Clearance kept between the sticker and the panel.</summary>
        public const float PanelGap = 40f;

        /// <summary>Scale the sticker zooms in from.</summary>
        private const float StartScale = 0.85f;

        /// <summary>Seconds the fade and zoom take.</summary>
        private const float AppearDuration = 0.4f;

        /// <summary>
        /// Smallest scale a sticker may be shrunk to so it fits next to the panel; below it, no
        /// sticker is shown.
        /// </summary>
        internal const float MinFitScale = 0.3f;

        /// <summary>
        /// Share of the largest available scale a region must allow to be picked, so a sticker
        /// that could be shown large is never put in a cramped corner instead.
        /// </summary>
        private const float RoomiestShare = 0.8f;

        /// <summary>Smallest share of its fitted size a sticker is drawn at; each one rolls its own.</summary>
        internal const float MinSizeShare = 0.8f;

        /// <summary>
        /// Creates a sticker in the free space around the panel, or <see langword="null"/> when
        /// no side of it has room for one.
        /// </summary>
        /// <param name="panel">What the result panel paints, in design coordinates.</param>
        /// <param name="area">Where the sticker may go: what the screen shows, in design coordinates.</param>
        /// <returns>The sticker, already appearing, or <see langword="null"/>.</returns>
        public static Image Create(Rectangle panel, Rectangle area)
        {
            int costume = MathHelper.RND_RANGE(0, Costumes - 1);
            Image sticker = Image.FromResource(Resources.Img.MenuResultScreenHalloween, costume);
            float tilt = MathHelper.FLOAT_RND_RANGE((int)MinTilt, (int)MaxTilt) * (MathHelper.RND(1) == 0 ? -1f : 1f);
            if (!TryPlace(panel, area, sticker.width, sticker.height, tilt, out float x, out float y, out float scale))
            {
                return null;
            }

            sticker.anchor = 18;
            sticker.parentAnchor = 9;
            sticker.touchable = false;
            sticker.x = x;
            sticker.y = y;
            sticker.rotation = tilt;
            sticker.color = RGBAColor.transparentRGBA;
            sticker.scaleX = sticker.scaleY = scale * StartScale;
            Timeline appear = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            appear.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0f));
            appear.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, AppearDuration));
            appear.AddKeyFrame(KeyFrame.MakeScale(scale * StartScale, scale * StartScale, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0f));
            appear.AddKeyFrame(KeyFrame.MakeScale(scale, scale, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, AppearDuration));
            sticker.PlayTimeline(sticker.AddTimeline(appear));
            return sticker;
        }

        /// <summary>
        /// Picks a spot for a sticker clear of the panel: a random free region with room for it -
        /// left or right of the panel on a wide screen, above or below it on a tall one - then a
        /// random point there that keeps the whole tilted sticker inside the region. A region too
        /// small for the full size shrinks the sticker to fit, down to <see cref="MinFitScale"/>;
        /// only regions close to the roomiest are picked from. The sticker is then drawn at a
        /// random <see cref="MinSizeShare"/> to all of the size that fits.
        /// </summary>
        /// <param name="panel">What the result panel paints.</param>
        /// <param name="area">Where the sticker may go.</param>
        /// <param name="width">Sticker width, untilted.</param>
        /// <param name="height">Sticker height, untilted.</param>
        /// <param name="tilt">Tilt in degrees.</param>
        /// <param name="x">Center X of the chosen spot.</param>
        /// <param name="y">Center Y of the chosen spot.</param>
        /// <param name="scale">Scale to draw the sticker at, at most 1.</param>
        /// <returns>Whether any region has room.</returns>
        internal static bool TryPlace(
            Rectangle panel,
            Rectangle area,
            float width,
            float height,
            float tilt,
            out float x,
            out float y,
            out float scale)
        {
            float radians = tilt * MathF.PI / 180f;
            float cos = MathF.Abs(MathF.Cos(radians));
            float sin = MathF.Abs(MathF.Sin(radians));
            float boundsW = (width * cos) + (height * sin);
            float boundsH = (width * sin) + (height * cos);

            float areaRight = area.x + area.w;
            float areaBottom = area.y + area.h;
            List<(Rectangle Region, float Scale)> regions = [];
            AddRegion(regions, area.x, area.y, panel.x - PanelGap, areaBottom, boundsW, boundsH);
            AddRegion(regions, panel.x + panel.w + PanelGap, area.y, areaRight, areaBottom, boundsW, boundsH);
            AddRegion(regions, area.x, area.y, areaRight, panel.y - PanelGap, boundsW, boundsH);
            AddRegion(regions, area.x, panel.y + panel.h + PanelGap, areaRight, areaBottom, boundsW, boundsH);
            if (regions.Count == 0)
            {
                x = y = scale = 0f;
                return false;
            }

            float best = 0f;
            foreach ((_, float regionFit) in regions)
            {
                best = MathF.Max(best, regionFit);
            }
            _ = regions.RemoveAll(entry => entry.Scale < best * RoomiestShare);
            (Rectangle region, float fit) = regions[MathHelper.RND_RANGE(0, regions.Count - 1)];
            scale = MathF.Max(MinFitScale, fit * (MinSizeShare + ((1f - MinSizeShare) * MathHelper.RND_0_1)));
            float halfW = boundsW * scale / 2f;
            float halfH = boundsH * scale / 2f;
            x = Between(region.x + halfW, region.x + region.w - halfW);
            y = Between(region.y + halfH, region.y + region.h - halfH);
            return true;
        }

        /// <summary>Adds a free region to pick from when a sticker fits in it at a usable scale.</summary>
        /// <param name="regions">Regions found so far.</param>
        /// <param name="left">Left edge of the region.</param>
        /// <param name="top">Top edge of the region.</param>
        /// <param name="right">Right edge of the region.</param>
        /// <param name="bottom">Bottom edge of the region.</param>
        /// <param name="boundsW">Width of the tilted sticker.</param>
        /// <param name="boundsH">Height of the tilted sticker.</param>
        private static void AddRegion(
            List<(Rectangle Region, float Scale)> regions,
            float left,
            float top,
            float right,
            float bottom,
            float boundsW,
            float boundsH)
        {
            float scale = MathF.Min(1f, MathF.Min((right - left) / boundsW, (bottom - top) / boundsH));
            if (scale >= MinFitScale)
            {
                regions.Add((new Rectangle(left, top, right - left, bottom - top), scale));
            }
        }

        /// <summary>A random value between two bounds.</summary>
        /// <param name="low">Lower bound.</param>
        /// <param name="high">Upper bound; equal to or above <paramref name="low"/>.</param>
        /// <returns>The random value.</returns>
        private static float Between(float low, float high)
        {
            return low + (MathHelper.RND_0_1 * MathF.Max(0f, high - low));
        }
    }
}
