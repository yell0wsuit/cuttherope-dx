using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Physics;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

using Vector3 = System.Numerics.Vector3;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Physics-backed rope used for bungee connections, rope cutting, and seasonal rope rendering.
    /// </summary>
    internal sealed class Bungee : ConstraintSystem
    {
        /// <summary>
        /// Draws a single antialiased line segment, continuing from the previous segment's edge vertices.
        /// </summary>
        /// <param name="x1">Start X coordinate.</param>
        /// <param name="y1">Start Y coordinate.</param>
        /// <param name="x2">End X coordinate.</param>
        /// <param name="y2">End Y coordinate.</param>
        /// <param name="size">Half-width of the line.</param>
        /// <param name="color">Line color.</param>
        /// <param name="lx">Left edge X from the previous segment; set to <c>-1</c> for the first segment.</param>
        /// <param name="ly">Left edge Y from the previous segment.</param>
        /// <param name="rx">Right edge X from the previous segment.</param>
        /// <param name="ry">Right edge Y from the previous segment.</param>
        /// <param name="highlighted">Whether to render with additive highlight blending.</param>
        private static void DrawAntialiasedLineContinued(float x1, float y1, float x2, float y2, float size, RGBAColor color, ref float lx, ref float ly, ref float rx, ref float ry, bool highlighted)
        {
            Vector start = Vect(x1, y1);
            Vector end = Vect(x2, y2);
            Vector span = VectSub(end, start);
            if (!VectEqual(span, vectZero))
            {
                Vector drawSpan = highlighted ? span : VectMult(span, color.AlphaChannel == 1f ? 1.02f : 1f);
                Vector leftStart = VectPerp(span);
                Vector normal = VectNormalize(leftStart);
                leftStart = VectMult(normal, size);
                Vector rightStart = VectNeg(leftStart);
                Vector carryLeftEnd = VectAdd(leftStart, span);
                Vector carryRightEnd = VectAdd(rightStart, span);
                carryLeftEnd = VectAdd(carryLeftEnd, start);
                carryRightEnd = VectAdd(carryRightEnd, start);
                Vector leftEnd = VectAdd(leftStart, drawSpan);
                Vector rightEnd = VectAdd(rightStart, drawSpan);
                Vector leftOuterStart = VectMult(normal, size + 6f);
                Vector rightOuterStart = VectNeg(leftOuterStart);
                Vector leftOuterEnd = VectAdd(leftOuterStart, span);
                Vector rightOuterEnd = VectAdd(rightOuterStart, span);
                leftOuterStart = VectAdd(leftOuterStart, start);
                rightOuterStart = VectAdd(rightOuterStart, start);
                leftOuterEnd = VectAdd(leftOuterEnd, start);
                rightOuterEnd = VectAdd(rightOuterEnd, start);
                if (lx == -1f)
                {
                    leftStart = VectAdd(leftStart, start);
                    rightStart = VectAdd(rightStart, start);
                }
                else
                {
                    leftStart = Vect(lx, ly);
                    rightStart = Vect(rx, ry);
                }
                leftEnd = VectAdd(leftEnd, start);
                rightEnd = VectAdd(rightEnd, start);
                lx = carryLeftEnd.X;
                ly = carryLeftEnd.Y;
                rx = carryRightEnd.X;
                ry = carryRightEnd.Y;
                Vector leftInnerStart = VectSub(leftStart, normal);
                Vector leftInnerEnd = VectSub(leftEnd, normal);
                Vector rightInnerStart = VectAdd(rightStart, normal);
                Vector rightInnerEnd = VectAdd(rightEnd, normal);
                float[] glowPositions = GetFloatCache(ref s_glowPositionsCache, 16);
                int pointerIndex = 0;
                WritePair(glowPositions, ref pointerIndex, leftOuterStart);
                WritePair(glowPositions, ref pointerIndex, leftOuterEnd);
                WritePair(glowPositions, ref pointerIndex, leftStart);
                WritePair(glowPositions, ref pointerIndex, leftEnd);
                WritePair(glowPositions, ref pointerIndex, rightStart);
                WritePair(glowPositions, ref pointerIndex, rightEnd);
                WritePair(glowPositions, ref pointerIndex, rightOuterStart);
                WritePair(glowPositions, ref pointerIndex, rightOuterEnd);
                RGBAColor whiteRGBA = RGBAColor.whiteRGBA;
                whiteRGBA.AlphaChannel = 0.1f * color.AlphaChannel;
                glowColors[2] = whiteRGBA;
                glowColors[3] = whiteRGBA;
                glowColors[4] = whiteRGBA;
                glowColors[5] = whiteRGBA;
                float[] innerPositions = GetFloatCache(ref s_innerPositionsCache, 20);
                int pointer2Index = 0;
                WritePair(innerPositions, ref pointer2Index, leftStart);
                WritePair(innerPositions, ref pointer2Index, leftEnd);
                WritePair(innerPositions, ref pointer2Index, leftInnerStart);
                WritePair(innerPositions, ref pointer2Index, leftInnerEnd);
                WritePair(innerPositions, ref pointer2Index, start);
                WritePair(innerPositions, ref pointer2Index, end);
                WritePair(innerPositions, ref pointer2Index, rightInnerStart);
                WritePair(innerPositions, ref pointer2Index, rightInnerEnd);
                WritePair(innerPositions, ref pointer2Index, rightStart);
                WritePair(innerPositions, ref pointer2Index, rightEnd);
                RGBAColor rgbaColor = color;
                float highlightAdditive = 0.15f * color.AlphaChannel;
                color.RedColor += highlightAdditive;
                color.GreenColor += highlightAdditive;
                color.BlueColor += highlightAdditive;
                innerColors[2] = color;
                innerColors[3] = color;
                innerColors[4] = rgbaColor;
                innerColors[5] = rgbaColor;
                innerColors[6] = color;
                innerColors[7] = color;
                if (highlighted)
                {
                    Renderer.SetBlendFunc(BlendingFactor.GLSRCALPHA, BlendingFactor.GLONE);
                    VertexPositionColor[] highlightVertices = BuildColoredVertices(glowPositions, glowColors, 8);
                    Renderer.DrawTriangleStrip(highlightVertices, 8);
                }
                Renderer.SetBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
                VertexPositionColor[] mainVertices = BuildColoredVertices(innerPositions, innerColors, 10);
                Renderer.DrawTriangleStrip(mainVertices, 10);
            }
        }

        /// <summary>
        /// Builds an array of colored vertices from parallel position and color arrays.
        /// </summary>
        /// <param name="positions">Flat array of X/Y coordinate pairs.</param>
        /// <param name="colors">Per-vertex colors.</param>
        /// <param name="vertexCount">Number of vertices to build.</param>
        /// <returns>The populated vertex array.</returns>
        private static VertexPositionColor[] BuildColoredVertices(float[] positions, RGBAColor[] colors, int vertexCount)
        {
            VertexPositionColor[] vertices = GetVertexCache(ref s_bungeeVerticesCache, vertexCount);
            int positionIndex = 0;
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 position = new(positions[positionIndex++], positions[positionIndex++], 0f);
                vertices[i] = new VertexPositionColor(position, colors[i].ToColor());
            }
            return vertices;
        }

        /// <summary>
        /// Returns a cached vertex array, reallocating if the cache is too small.
        /// </summary>
        /// <param name="cache">Reference to the cached array.</param>
        /// <param name="vertexCount">Minimum required capacity.</param>
        /// <returns>The cached or newly allocated array.</returns>
        private static VertexPositionColor[] GetVertexCache(ref VertexPositionColor[] cache, int vertexCount)
        {
            if (cache == null || cache.Length < vertexCount)
            {
                cache = new VertexPositionColor[vertexCount];
            }
            return cache;
        }

        /// <summary>
        /// Returns a cached float array, reallocating if the cache is too small.
        /// </summary>
        /// <param name="cache">Reference to the cached array.</param>
        /// <param name="length">Minimum required capacity.</param>
        /// <returns>The cached or newly allocated array.</returns>
        private static float[] GetFloatCache(ref float[] cache, int length)
        {
            if (cache == null || cache.Length < length)
            {
                cache = new float[length];
            }
            return cache;
        }

        /// <summary>
        /// Writes a vector's X and Y components into a float buffer at the current index.
        /// </summary>
        /// <param name="buffer">The target float array.</param>
        /// <param name="index">Write position; advanced by 2 after the call.</param>
        /// <param name="v">The vector to write.</param>
        private static void WritePair(float[] buffer, ref int index, Vector v)
        {
            buffer[index++] = v.X;
            buffer[index++] = v.Y;
        }

        /// <summary>
        /// Draws an entire bungee rope by sampling a bezier curve through the given constraint points.
        /// </summary>
        /// <param name="b">The bungee instance being drawn.</param>
        /// <param name="pts">Array of constraint point positions along the rope.</param>
        /// <param name="count">Number of valid points in <paramref name="pts"/>.</param>
        /// <param name="points">Number of bezier sample points per segment.</param>
        private static void DrawBungee(Bungee b, Vector[] pts, int count, int points)
        {
            float alphaMultiplier = GetCutFadeAlpha(b);
            float stretchRedThreshold = ActivePhysicsConstants.BungeeStretchRedThreshold;
            float segmentLength = VectDistance(Vect(pts[0].X, pts[0].Y), Vect(pts[1].X, pts[1].Y));

            // Get selected rope colors from preferences
            int selectedRopeIndex = Preferences.GetIntForKey("PREFS_SELECTED_ROPE");
            RopeColorHelper.RopeDrawColors drawColors = RopeColorHelper.GetDrawColors(
                selectedRopeIndex,
                alphaMultiplier,
                b.highlighted,
                segmentLength,
                BUNGEE_REST_LEN,
                stretchRedThreshold);
            RGBAColor baseColor1 = drawColors.BaseColor1;
            RGBAColor baseColor2 = drawColors.BaseColor2;
            RGBAColor shadeColor1 = drawColors.ShadeColor1;
            RGBAColor shadeColor2 = drawColors.ShadeColor2;

            float relaxThresholdSoft = ActivePhysicsConstants.BungeeRelaxThresholdSoft;
            float relaxThresholdMedium = ActivePhysicsConstants.BungeeRelaxThresholdMedium;
            float relaxThresholdHard = ActivePhysicsConstants.BungeeRelaxThresholdHard;
            b.relaxed = segmentLength <= BUNGEE_REST_LEN + relaxThresholdSoft
                ? 0
                : segmentLength <= BUNGEE_REST_LEN + relaxThresholdMedium
                    ? 1
                    : segmentLength <= BUNGEE_REST_LEN + relaxThresholdHard ? 2 : 3;
            bool useAlternateStripe = false;
            int sampleCount = (count - 1) * points;
            float[] pointBuffer = new float[sampleCount * 2];
            b.drawPtsCount = sampleCount * 2;
            float sampleStep = 1f / sampleCount;

            // Draw outline for non-default rope skins
            if (selectedRopeIndex >= 1 && count >= 3 && sampleCount > 0)
            {
                int outlineMaxPts = sampleCount + 2;
                float[] outlinePts = new float[outlineMaxPts * 2];
                int outlinePtCount = 0;
                float outlineT = 0f;
                for (; ; )
                {
                    if (outlineT > 1)
                    {
                        outlineT = 1f;
                    }
                    if (outlinePtCount + 2 > outlinePts.Length)
                    {
                        break;
                    }
                    Vector v = DrawHelper.CalcPathBezier(pts, count, outlineT);
                    outlinePts[outlinePtCount++] = v.X;
                    outlinePts[outlinePtCount++] = v.Y;
                    if (outlineT >= 1f)
                    {
                        break;
                    }
                    outlineT += sampleStep;
                }
                float olx = -1f, oly = -1f, orx = -1f, ory = -1f;
                RGBAColor outlineColor = RGBAColor.MakeRGBA(0, 0, 0, 0.4f * alphaMultiplier);
                Renderer.SetColor(outlineColor.ToColor());
                int ptCount = outlinePtCount / 2;
                for (int i = 0; i < ptCount - 1; i++)
                {
                    int idx = i * 2;
                    DrawAntialiasedLineContinued(outlinePts[idx], outlinePts[idx + 1], outlinePts[idx + 2], outlinePts[idx + 3], 7f, outlineColor, ref olx, ref oly, ref orx, ref ory, false);
                }
            }

            float bezierT = 0f;
            int cachedPointCount = 0;
            int drawPointCount = 0;
            RGBAColor stripeColor1 = shadeColor1;
            RGBAColor stripeColor2 = shadeColor2;
            float redStep = (baseColor1.RedColor - shadeColor1.RedColor) / (sampleCount - 1);
            float greenStep = (baseColor1.GreenColor - shadeColor1.GreenColor) / (sampleCount - 1);
            float blueStep = (baseColor1.BlueColor - shadeColor1.BlueColor) / (sampleCount - 1);
            float redStepAlt = (baseColor2.RedColor - shadeColor2.RedColor) / (sampleCount - 1);
            float greenStepAlt = (baseColor2.GreenColor - shadeColor2.GreenColor) / (sampleCount - 1);
            float blueStepAlt = (baseColor2.BlueColor - shadeColor2.BlueColor) / (sampleCount - 1);
            float lx = -1f;
            float ly = -1f;
            float rx = -1f;
            float ry = -1f;
            for (; ; )
            {
                if (bezierT > 1)
                {
                    bezierT = 1f;
                }
                if (count < 3)
                {
                    break;
                }
                Vector vector = DrawHelper.CalcPathBezier(pts, count, bezierT);
                pointBuffer[cachedPointCount++] = vector.X;
                pointBuffer[cachedPointCount++] = vector.Y;
                b.drawPts[drawPointCount++] = vector.X;
                b.drawPts[drawPointCount++] = vector.Y;
                if (cachedPointCount >= 8 || bezierT == 1)
                {
                    RGBAColor color = b.forceWhite ? RGBAColor.whiteRGBA : !useAlternateStripe ? stripeColor2 : stripeColor1;
                    Renderer.SetColor(color.ToColor());
                    int segmentCount = cachedPointCount >> 1;
                    for (int i = 0; i < segmentCount - 1; i++)
                    {
                        DrawAntialiasedLineContinued(pointBuffer[i * 2], pointBuffer[(i * 2) + 1], pointBuffer[(i * 2) + 2], pointBuffer[(i * 2) + 3], 5f, color, ref lx, ref ly, ref rx, ref ry, b.highlighted);
                    }
                    pointBuffer[0] = pointBuffer[cachedPointCount - 2];
                    pointBuffer[1] = pointBuffer[cachedPointCount - 1];
                    cachedPointCount = 2;
                    useAlternateStripe = !useAlternateStripe;
                    stripeColor1.RedColor += redStep * (segmentCount - 1);
                    stripeColor1.GreenColor += greenStep * (segmentCount - 1);
                    stripeColor1.BlueColor += blueStep * (segmentCount - 1);
                    stripeColor2.RedColor += redStepAlt * (segmentCount - 1);
                    stripeColor2.GreenColor += greenStepAlt * (segmentCount - 1);
                    stripeColor2.BlueColor += blueStepAlt * (segmentCount - 1);
                }
                if (bezierT == 1)
                {
                    break;
                }
                bezierT += sampleStep;
            }

            b.drawPtsCount = drawPointCount;
            b.DrawChristmasLights(alphaMultiplier);
        }

        /// <summary>
        /// Returns the fade alpha for cut rope and chain draw paths.
        /// </summary>
        /// <param name="b">The bungee instance being drawn.</param>
        /// <returns>Opaque before cutting or during the force-white cut frame, then fades by remaining cut time.</returns>
        internal static float GetCutFadeAlpha(Bungee b)
        {
            return b.cut == -1 || b.forceWhite ? 1f : b.cutTime / 1.95f;
        }

        /// <summary>
        /// Returns the straight-alpha blend factors used while fading chain textures.
        /// </summary>
        /// <returns>Source and destination blend factors for chain texture fade.</returns>
        internal static (BlendingFactor Source, BlendingFactor Destination) GetChainFadeBlendFactors()
        {
            return (BlendingFactor.GLSRCALPHA, BlendingFactor.GLONEMINUSSRCALPHA);
        }

        /// <summary>
        /// Builds per-vertex colors for chain sprites, reproducing the original renderer's per-link
        /// masking: each link randomly stays opaque white or is tinted with a "chain mask" shade.
        /// The RGB choice is stable per link (driven by <paramref name="seed" /> + link index, so it
        /// matches the original's generate-once-and-cache behavior without flickering across frames
        /// or head/tail draw calls), while the alpha is always the current fade value.
        /// </summary>
        /// <param name="spriteCount">Number of chain sprites.</param>
        /// <param name="pointSpriteCount">Number of leading point sprites, which shade their last corner.</param>
        /// <param name="alpha">Alpha to apply to each vertex.</param>
        /// <param name="seed">Per-bungee seed selecting which links are masked.</param>
        /// <returns>Four colors per sprite.</returns>
        internal static RGBAColor[] BuildChainSpriteColors(int spriteCount, int pointSpriteCount, float alpha, int seed)
        {
            RGBAColor[] colors = new RGBAColor[spriteCount * 4];
            for (int i = 0; i < spriteCount; i++)
            {
                uint hash = HashChainSprite(seed, i);
                RGBAColor color = (hash & 1) != 0 ? RGBAColor.whiteRGBA : GetChainMaskColor(hash);
                color.AlphaChannel = alpha;
                int baseIndex = i * 4;
                colors[baseIndex] = color;
                colors[baseIndex + 1] = color;
                colors[baseIndex + 2] = color;

                // Point sprites leave their fourth corner opaque white, so a masked link shades
                // across the quad instead of being flat; midpoint sprites tint all four corners.
                RGBAColor lastCorner = i < pointSpriteCount ? RGBAColor.whiteRGBA : color;
                lastCorner.AlphaChannel = alpha;
                colors[baseIndex + 3] = lastCorner;
            }
            return colors;
        }

        /// <summary>
        /// Returns the per-link tint applied to the masked half of the chain sprites.
        /// </summary>
        /// <remarks>
        /// The unmasked half of the links stays opaque white (<c>solidOpaqueRGBA</c>); the rest pick
        /// one of the three shades <c>Bungee::getChainMaskColor</c> indexes with <c>rand() % 3</c>.
        /// </remarks>
        /// <param name="hash">Stable per-link hash selecting the shade.</param>
        /// <returns>An opaque shade; alpha is overwritten by the caller.</returns>
        private static RGBAColor GetChainMaskColor(uint hash)
        {
            int index = (int)((hash >> 1) % (uint)ChainMaskRed.Length);
            return RGBAColor.MakeRGBA(ChainMaskRed[index], ChainMaskGreen[index], ChainMaskBlue[index], 1f);
        }

        /// <summary>
        /// Produces a stable pseudo-random hash for a chain link from the bungee seed and link index.
        /// </summary>
        /// <param name="seed">Per-bungee seed.</param>
        /// <param name="index">Link index.</param>
        /// <returns>A well-mixed 32-bit hash.</returns>
        private static uint HashChainSprite(int seed, int index)
        {
            uint h = (uint)(seed * 73856093) ^ (uint)((index + 1) * 19349663);
            h ^= h >> 13;
            h *= 0x5BD1E995u;
            h ^= h >> 15;
            return h;
        }

        /// <summary>
        /// Builds the chain sprite layout: one sprite at each sampled curve point
        /// and a second sprite centered between adjacent samples.
        /// </summary>
        /// <param name="pts">Control points along the chain.</param>
        /// <param name="count">Number of valid control points.</param>
        /// <param name="points">Number of bezier samples per control-point segment.</param>
        /// <param name="pointSpriteSize">Size of the sprite drawn at sampled points.</param>
        /// <param name="midpointSpriteSize">Size of the sprite drawn between sampled points.</param>
        /// <returns>The chain sprites to submit in draw order.</returns>
        internal static ChainSprite[] BuildChainSpritePlan(Vector[] pts, int count, int points, Vector pointSpriteSize, Vector midpointSpriteSize)
        {
            int sampleCount = (count - 1) * points;
            if (pts == null || count < 2 || sampleCount <= 0)
            {
                return [];
            }

            Vector[] samples = new Vector[sampleCount];
            float bezierT = 0f;
            float sampleStep = 1f / sampleCount;
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = DrawHelper.CalcPathBezier(pts, count, bezierT);
                bezierT += sampleStep;
            }

            ChainSprite[] sprites = new ChainSprite[sampleCount + Math.Max(0, sampleCount - 1)];
            int spriteIndex = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                float angle = i == 0 ? 0f : GetChainAngle(samples[i - 1], samples[i]);
                sprites[spriteIndex++] = new ChainSprite(ChainPointQuad, samples[i], angle, CreateCenteredRotatedQuad(samples[i], pointSpriteSize, angle));
            }
            for (int i = 0; i < sampleCount - 1; i++)
            {
                Vector center = Vect(
                    samples[i].X + ((samples[i + 1].X - samples[i].X) * 0.5f),
                    samples[i].Y + ((samples[i + 1].Y - samples[i].Y) * 0.5f));
                float angle = GetChainAngle(samples[i], samples[i + 1]);
                sprites[spriteIndex++] = new ChainSprite(ChainMidpointQuad, center, angle, CreateCenteredRotatedQuad(center, midpointSpriteSize, angle));
            }
            return sprites;
        }

        /// <summary>
        /// Draws a chain bungee using the two separate chain sprites.
        /// </summary>
        /// <param name="b">The bungee instance being drawn.</param>
        /// <param name="pts">Control points along the chain.</param>
        /// <param name="count">Number of valid control points.</param>
        /// <param name="points">Number of bezier samples per control-point segment.</param>
        private static void DrawChain(Bungee b, Vector[] pts, int count, int points)
        {
            Texture2D texture = Application.GetTexture(Resources.Img.ObjExpChain);
            if (texture?.quadRects == null || texture.quads == null || texture.quadsCount < 2)
            {
                DrawBungee(b, pts, count, points);
                return;
            }

            ChainSprite[] sprites = BuildChainSpritePlan(
                pts,
                count,
                points,
                Vect(texture.quadRects[ChainPointQuad].w, texture.quadRects[ChainPointQuad].h),
                Vect(texture.quadRects[ChainMidpointQuad].w, texture.quadRects[ChainMidpointQuad].h));
            if (sprites.Length == 0)
            {
                return;
            }

            Quad3D[] vertices = new Quad3D[sprites.Length];
            Quad2D[] texCoordinates = new Quad2D[sprites.Length];
            int pointSpriteCount = 0;
            for (int i = 0; i < sprites.Length; i++)
            {
                ChainSprite sprite = sprites[i];
                vertices[i] = sprite.VertexQuad;
                texCoordinates[i] = texture.quads[sprite.QuadIndex];
                if (sprite.QuadIndex == ChainPointQuad)
                {
                    pointSpriteCount++;
                }
            }

            VertexPositionColorTexture[] vertexBuffer = new VertexPositionColorTexture[sprites.Length * 4];
            short[] indices = BuildQuadIndices(sprites.Length);
            RGBAColor[] colors = BuildChainSpriteColors(sprites.Length, pointSpriteCount, GetCutFadeAlpha(b), b.ChainColorSeed);
            Renderer.FillTexturedColoredVertices(vertices, texCoordinates, colors, vertexBuffer, sprites.Length);

            Renderer.SetColor(RGBAColor.whiteRGBA.ToColor());
            (BlendingFactor source, BlendingFactor destination) = GetChainFadeBlendFactors();
            Renderer.SetBlendFunc(source, destination);
            Renderer.Enable(Renderer.GL_TEXTURE_2D);
            Renderer.BindTexture(texture);
            Renderer.DrawTriangleList(vertexBuffer, indices, indices.Length);
            Renderer.SetBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
        }

        /// <summary>
        /// Builds triangle-list indices for a sequence of quads.
        /// </summary>
        /// <param name="quadCount">Number of quads.</param>
        /// <returns>Six indices per quad.</returns>
        private static short[] BuildQuadIndices(int quadCount)
        {
            short[] indices = new short[quadCount * 6];
            for (int i = 0; i < quadCount; i++)
            {
                indices[i * 6] = (short)(i * 4);
                indices[(i * 6) + 1] = (short)((i * 4) + 1);
                indices[(i * 6) + 2] = (short)((i * 4) + 2);
                indices[(i * 6) + 3] = (short)((i * 4) + 3);
                indices[(i * 6) + 4] = (short)((i * 4) + 2);
                indices[(i * 6) + 5] = (short)((i * 4) + 1);
            }
            return indices;
        }

        /// <summary>
        /// Returns the sprite angle used by the original chain renderer for a segment.
        /// </summary>
        /// <param name="previous">Previous sample point.</param>
        /// <param name="current">Current sample point.</param>
        /// <returns>Rotation in radians.</returns>
        private static float GetChainAngle(Vector previous, Vector current)
        {
            return MathF.Atan2(previous.Y - current.Y, previous.X - current.X) + (MathF.PI / 2f);
        }

        /// <summary>
        /// Creates a rotated vertex quad centered on <paramref name="center"/>.
        /// </summary>
        /// <param name="center">Sprite center in world coordinates.</param>
        /// <param name="size">Sprite size.</param>
        /// <param name="angle">Rotation in radians.</param>
        /// <returns>The rotated quad vertices.</returns>
        private static Quad3D CreateCenteredRotatedQuad(Vector center, Vector size, float angle)
        {
            float halfWidth = size.X * 0.5f;
            float halfHeight = size.Y * 0.5f;
            Vector bl = Vect(center.X - halfWidth, center.Y - halfHeight);
            Vector br = Vect(center.X + halfWidth, center.Y - halfHeight);
            Vector tl = Vect(center.X - halfWidth, center.Y + halfHeight);
            Vector tr = Vect(center.X + halfWidth, center.Y + halfHeight);
            if (angle != 0f)
            {
                bl = RotateAround(bl, angle, center);
                br = RotateAround(br, angle, center);
                tl = RotateAround(tl, angle, center);
                tr = RotateAround(tr, angle, center);
            }
            return Quad3D.MakeQuad3DEx(bl.X, bl.Y, br.X, br.Y, tl.X, tl.Y, tr.X, tr.Y);
        }

        /// <summary>
        /// Rotates <paramref name="point"/> around <paramref name="center"/> without relying on global math lookup tables.
        /// </summary>
        /// <param name="point">Point to rotate.</param>
        /// <param name="angle">Rotation in radians.</param>
        /// <param name="center">Rotation center.</param>
        /// <returns>The rotated point.</returns>
        private static Vector RotateAround(Vector point, float angle, Vector center)
        {
            float dx = point.X - center.X;
            float dy = point.Y - center.Y;
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            return Vect(
                center.X + (dx * cos) - (dy * sin),
                center.Y + (dx * sin) + (dy * cos));
        }

        /// <summary>
        /// Initializes a bungee rope between head and tail constraint points.
        /// </summary>
        /// <param name="h">Optional existing head constraint point; a new anchor is created when this is <see langword="null"/>.</param>
        /// <param name="hx">Initial head X position.</param>
        /// <param name="hy">Initial head Y position.</param>
        /// <param name="t">Optional existing tail constraint point; a new tail is created when this is <see langword="null"/>.</param>
        /// <param name="tx">Initial tail X position.</param>
        /// <param name="ty">Initial tail Y position.</param>
        /// <param name="len">Initial rope length used to roll out intermediate rope segments.</param>
        /// <returns>The initialized bungee instance.</returns>
        public Bungee InitWithHeadAtXYTailAtTXTYandLength(ConstrainedPoint h, float hx, float hy, ConstrainedPoint t, float tx, float ty, float len)
        {
            relaxationTimes = 30;
            lineWidth = 10f;
            cut = -1;
            bungeeMode = 0;
            highlighted = false;
            bungeeAnchor = h ?? new ConstrainedPoint();
            ownsAnchor = h == null;
            if (t != null)
            {
                tail = t;
                ownsTail = false;
            }
            else
            {
                tail = new ConstrainedPoint();
                tail.SetWeight(1f);
                ownsTail = true;
            }
            if (ownsAnchor)
            {
                bungeeAnchor.SetWeight(0.02f);
            }
            bungeeAnchor.pos = Vect(hx, hy);
            tail.pos = Vect(tx, ty);
            AddPart(bungeeAnchor);
            AddPart(tail);
            tail.AddConstraintwithRestLengthofType(bungeeAnchor, BUNGEE_REST_LEN, ConstraintType.DISTANCE);
            Vector v = VectSub(tail.pos, bungeeAnchor.pos);
            int subdivisionCount = (int)((len / BUNGEE_REST_LEN) + 2f);
            v = VectDiv(v, subdivisionCount);
            RollplacingWithOffset(len, v);
            forceWhite = false;
            initialCandleAngle = -1f;
            chosenOne = false;
            hideTailParts = false;
            breakable = false;
            return this;
        }

        /// <summary>
        /// Marks this bungee as a chain: it renders as a chain and can only be cut by the Time Travel
        /// axe (the original's single <c>isUnBreakable</c> flag), ignoring finger/razor cuts.
        /// </summary>
        public void SetCutOnlyByAxe()
        {
            breakable = true;
            cutOnlyByAxe = true;
        }

        /// <summary>
        /// Calculates the current polyline length across all bungee constraint points.
        /// </summary>
        /// <returns>The approximate current bungee length in world units.</returns>
        public int GetLength()
        {
            int totalLength = 0;
            Vector pos = vectZero;
            int count = parts.Count;
            for (int i = 0; i < count; i++)
            {
                ConstrainedPoint constrainedPoint = parts[i];
                if (i > 0)
                {
                    totalLength += (int)VectDistance(pos, constrainedPoint.pos);
                }
                pos = constrainedPoint.pos;
            }
            return totalLength;
        }

        /// <summary>
        /// Rolls additional rope length into the bungee without applying a placement offset.
        /// </summary>
        /// <param name="rollLen">Amount of rope length to add.</param>
        public void Roll(float rollLen)
        {
            RollplacingWithOffset(rollLen, vectZero);
        }

        /// <summary>
        /// Rolls additional rope length into the bungee and places new segments using an offset.
        /// </summary>
        /// <param name="rollLen">Amount of rope length to add.</param>
        /// <param name="off">Offset applied when placing new intermediate constraint points.</param>
        public void RollplacingWithOffset(float rollLen, Vector off)
        {
            ConstrainedPoint i = parts[^2];
            int tailRestLength = (int)tail.RestLengthFor(i);
            while (rollLen > 0f)
            {
                if (rollLen >= BUNGEE_REST_LEN)
                {
                    ConstrainedPoint constrainedPoint = parts[^2];
                    ConstrainedPoint newPart = new();
                    newPart.SetWeight(0.02f);
                    newPart.pos = VectAdd(constrainedPoint.pos, off);
                    AddPartAt(newPart, parts.Count - 1);
                    tail.ChangeConstraintFromTowithRestLength(constrainedPoint, newPart, tailRestLength);
                    newPart.AddConstraintwithRestLengthofType(constrainedPoint, BUNGEE_REST_LEN, ConstraintType.DISTANCE);
                    rollLen -= BUNGEE_REST_LEN;
                }
                else
                {
                    int newRestLength = (int)(rollLen + tailRestLength);
                    if (newRestLength > BUNGEE_REST_LEN)
                    {
                        rollLen = BUNGEE_REST_LEN;
                        tailRestLength = (int)(newRestLength - BUNGEE_REST_LEN);
                    }
                    else
                    {
                        ConstrainedPoint secondToLastPart = parts[^2];
                        tail.ChangeRestLengthToFor(newRestLength, secondToLastPart);
                        rollLen = 0f;
                    }
                }
            }
        }

        /// <summary>
        /// Removes rope length from the tail side of the bungee.
        /// </summary>
        /// <param name="amount">Amount of rope length to remove.</param>
        /// <returns>Remaining amount that could not be removed.</returns>
        public float RollBack(float amount)
        {
            float remainingAmount = amount;
            ConstrainedPoint tailNeighbor = parts[^2];
            int currentRestLength = (int)tail.RestLengthFor(tailNeighbor);
            int partCount = parts.Count;
            while (remainingAmount > 0f)
            {
                if (remainingAmount >= BUNGEE_REST_LEN)
                {
                    ConstrainedPoint removedPart = parts[partCount - 2];
                    ConstrainedPoint newTailAnchor = parts[partCount - 3];
                    tail.ChangeConstraintFromTowithRestLength(removedPart, newTailAnchor, currentRestLength);
                    parts.RemoveAt(parts.Count - 2);
                    partCount--;
                    remainingAmount -= BUNGEE_REST_LEN;
                }
                else
                {
                    int nextRestLength = (int)(currentRestLength - remainingAmount);
                    if (nextRestLength < 1)
                    {
                        remainingAmount = BUNGEE_REST_LEN;
                        currentRestLength = (int)(BUNGEE_REST_LEN + nextRestLength + ActivePhysicsConstants.BungeeRollBackOverflowPadding);
                    }
                    else
                    {
                        ConstrainedPoint tailAnchor = parts[partCount - 2];
                        tail.ChangeRestLengthToFor(nextRestLength, tailAnchor);
                        remainingAmount = 0f;
                    }
                }
            }
            int count = tail.constraints.Count;
            for (int j = 0; j < count; j++)
            {
                Constraint constraint = tail.constraints[j];
                if (constraint != null && constraint.type == ConstraintType.NOT_MORE_THAN)
                {
                    constraint.restLength = (partCount - 1) * (BUNGEE_REST_LEN + ActivePhysicsConstants.BungeeConstraintSlack);
                }
            }
            return remainingAmount;
        }

        /// <summary>
        /// Removes or detaches a bungee segment and weakens the remaining free points.
        /// </summary>
        /// <param name="part">Index of the segment part to remove.</param>
        public void RemovePart(int part)
        {
            forceWhite = false;
            ConstrainedPoint cutPart = parts[part];
            ConstrainedPoint nextPart = part + 1 >= parts.Count ? null : parts[part + 1];
            if (nextPart == null)
            {
                cutPart.RemoveConstraints();
            }
            else
            {
                for (int i = 0; i < nextPart.constraints.Count; i++)
                {
                    Constraint constraint = nextPart.constraints[i];
                    if (constraint.cp == cutPart)
                    {
                        _ = nextPart.constraints.Remove(constraint);
                        ConstrainedPoint stubPart = new();
                        stubPart.SetWeight(1E-05f);
                        stubPart.pos = nextPart.pos;
                        stubPart.prevPos = nextPart.prevPos;
                        AddPartAt(stubPart, part + 1);
                        stubPart.AddConstraintwithRestLengthofType(cutPart, BUNGEE_REST_LEN, ConstraintType.DISTANCE);
                        break;
                    }
                }
            }
            for (int j = 0; j < parts.Count; j++)
            {
                ConstrainedPoint bungeePart = parts[j];
                // Don't weaken an endpoint the rope doesn't own: tail is always external, and a
                // non-owned head (a candy point in a candiesConnected link) must keep its mass.
                // Owned anchors (normal/kicked grabs) still go limp, as before.
                if (bungeePart != tail && (bungeePart != bungeeAnchor || ownsAnchor))
                {
                    bungeePart.SetWeight(1E-05f);
                }
            }
        }

        /// <summary>
        /// Marks the bungee as cut at a segment index and starts the cut fade.
        /// </summary>
        /// <param name="part">Index of the segment part where the bungee was cut.</param>
        public void SetCut(int part)
        {
            cut = part;
            cutTime = 2f;
            forceWhite = true;
        }

        /// <summary>
        /// Adds constraints that keep rope segments closer to the pinned anchor.
        /// </summary>
        public void Strengthen()
        {
            int count = parts.Count;
            for (int i = 0; i < count; i++)
            {
                ConstrainedPoint constrainedPoint = parts[i];
                if (constrainedPoint != null)
                {
                    if (bungeeAnchor.pin.X != -1f)
                    {
                        if (constrainedPoint != tail)
                        {
                            constrainedPoint.SetWeight(0.5f);
                        }
                        if (i != 0)
                        {
                            constrainedPoint.AddConstraintwithRestLengthofType(bungeeAnchor, i * (BUNGEE_REST_LEN + ActivePhysicsConstants.BungeeConstraintSlack), ConstraintType.NOT_MORE_THAN);
                        }
                    }
                    i++;
                }
            }
        }

        /// <inheritdoc />
        public override void Update(float delta)
        {
            Update(delta, 1f);
        }

        /// <summary>
        /// Updates bungee physics with a custom Verlet integration coefficient.
        /// </summary>
        /// <param name="delta">Elapsed time in seconds since the last update.</param>
        /// <param name="koeff">Coefficient passed to constraint-point physics updates.</param>
        public void Update(float delta, float koeff)
        {
            if (cutTime > 0)
            {
                _ = Mover.MoveVariableToTarget(ref cutTime, 0f, 1f, delta);
                if (cutTime < 1.95f && forceWhite)
                {
                    RemovePart(cut);
                }
            }
            int count = parts.Count;
            for (int i = 0; i < count; i++)
            {
                ConstrainedPoint constrainedPoint = parts[i];
                // Don't integrate an endpoint the rope doesn't own: tail is always external,
                // and a non-owned head (a candy point in a candiesConnected link) is integrated
                // by the candy system. Owned anchors (normal/kicked grabs) still integrate.
                if (constrainedPoint != tail && (constrainedPoint != bungeeAnchor || ownsAnchor))
                {
                    ConstrainedPoint.Qcpupdate(constrainedPoint, delta, koeff);
                }
            }
            for (int j = 0; j < relaxationTimes; j++)
            {
                int partCount = parts.Count;
                for (int k = 0; k < partCount; k++)
                {
                    ConstrainedPoint.SatisfyConstraints(parts[k]);
                }
            }
        }

        /// <inheritdoc />
        public override void Draw()
        {
            int count = parts.Count;
            int drawSamplePoints = ActivePhysicsConstants.BungeeDrawSamplePoints;
            int chainSamplePoints = ChainDrawSamplePoints;
            Renderer.SetColor(s_ropeDrawColor);
            if (cut == -1)
            {
                Vector[] points = new Vector[count];
                for (int i = 0; i < count; i++)
                {
                    ConstrainedPoint constrainedPoint = parts[i];
                    points[i] = constrainedPoint.pos;
                }
                s_lightCounter = 0;
                s_lightStartCoord = 8;
                s_lightEndSkip = 8;
                if (breakable)
                {
                    DrawChain(this, points, count, chainSamplePoints);
                }
                else
                {
                    DrawBungee(this, points, count, drawSamplePoints);
                }
                return;
            }
            Vector[] headPoints = new Vector[count];
            Vector[] tailPoints = new Vector[count];
            bool inTail = false;
            int tailPartCount = 0;
            for (int j = 0; j < count; j++)
            {
                ConstrainedPoint part = parts[j];
                bool connectedToPrevious = true;
                if (j > 0)
                {
                    ConstrainedPoint previousPart = parts[j - 1];
                    if (!part.HasConstraintTo(previousPart))
                    {
                        connectedToPrevious = false;
                    }
                }
                if (part.pin.X == -1f && !connectedToPrevious)
                {
                    inTail = true;
                    headPoints[j] = part.pos;
                }
                if (!inTail)
                {
                    headPoints[j] = part.pos;
                }
                else
                {
                    tailPoints[tailPartCount] = part.pos;
                    tailPartCount++;
                }
            }
            int headPartCount = count - tailPartCount;
            s_lightCounter = 0;
            s_lightSavedEnd = 0;
            if (headPartCount > 0)
            {
                s_lightStartCoord = 8;
                s_lightEndSkip = tailPartCount > 0 ? 0 : 8;
                if (breakable)
                {
                    DrawChain(this, headPoints, headPartCount, chainSamplePoints);
                }
                else
                {
                    DrawBungee(this, headPoints, headPartCount, drawSamplePoints);
                }
            }
            if (tailPartCount > 0 && !hideTailParts)
            {
                s_lightStartCoord = headPartCount > 0 ? s_lightSavedEnd : 8;
                if (breakable)
                {
                    DrawChain(this, tailPoints, tailPartCount, chainSamplePoints);
                }
                else
                {
                    DrawBungee(this, tailPoints, tailPartCount, drawSamplePoints);
                }
            }
        }

        /// <summary>
        /// Draws Christmas lights along the rope.
        /// Matches the original iOS implementation: lights are placed at every 6 bezier sample points
        /// (12 coord indices), skipping 4 points at the start and end of each segment.
        /// Uses static state (<see cref="s_lightStartCoord" />, <see cref="s_lightEndSkip" />, etc.) set by <see cref="Draw" /> to coordinate
        /// light placement across head/tail segments when the rope is cut.
        /// </summary>
        /// <param name="alpha">Alpha multiplier applied to the light sprites.</param>
        private void DrawChristmasLights(float alpha)
        {
            if (!SpecialEvents.IsXmas || drawPtsCount < 4 || drawPts == null || alpha <= 0f)
            {
                return;
            }

            Texture2D texture;
            try
            {
                texture = Application.GetTexture(Resources.Img.XmasLights);
            }
            catch
            {
                return;
            }

            Rectangle[] rects = texture.quadRects;
            int rectCount = texture.quadsCount > 0 ? texture.quadsCount : rects?.Length ?? 0;
            if (rectCount == 0)
            {
                return;
            }

            int totalCoords = drawPtsCount;
            int startCoord = s_lightStartCoord;
            int endCoord = totalCoords - s_lightEndSkip;

            if (startCoord >= endCoord)
            {
                return;
            }

            RGBAColor color = RGBAColor.whiteRGBA;
            if (alpha < 1f)
            {
                color.AlphaChannel = alpha;
            }
            Renderer.SetColor(color.ToColor());

            lightRandomSeed ??= christmasRandom.Next(0, 1000);

            int lightIdx = s_lightCounter;
            for (int i = startCoord; i < endCoord; i += 12)
            {
                if (lightsCount != -1 && lightIdx >= lightsCount)
                {
                    break;
                }

                if (i + 1 >= drawPts.Length)
                {
                    break;
                }

                float x = drawPts[i];
                float y = drawPts[i + 1];

                if (lightsCount == -1)
                {
                    lightFrames[lightIdx] = christmasRandom.Next(rectCount);
                }

                int rectIndex = lightFrames[lightIdx] % rectCount;
                Rectangle rect = rects[rectIndex];

                DrawHelper.DrawImagePart(texture, rect, x - (rect.w / 2f), y - (rect.h / 2f));

                // Save overflow for tail continuation
                s_lightSavedEnd = i + 12 - endCoord + 2;

                lightIdx++;
            }

            s_lightCounter = lightIdx;
            if (lightsCount == -1)
            {
                lightsCount = lightIdx;
            }

            Renderer.SetColor(RGBAColor.whiteRGBA.ToColor());
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (parts != null)
                {
                    if (!ownsTail && tail != null)
                    {
                        foreach (ConstrainedPoint part in parts)
                        {
                            if (part != tail)
                            {
                                tail.RemoveConstraint(part);
                            }
                        }
                    }
                    foreach (ConstrainedPoint part in parts)
                    {
                        bool ownsPart = (part == bungeeAnchor && ownsAnchor) || (part == tail && ownsTail) || (part != bungeeAnchor && part != tail);
                        if (ownsPart)
                        {
                            part?.Dispose();
                        }
                    }
                    parts = null;
                }
                bungeeAnchor = null;
                tail = null;
                drawPts = null;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Moves the anchor to a new position, shifting all rope parts by the same delta.
        /// </summary>
        /// <param name="newPos">New world-space position for the bungee anchor.</param>
        public void MoveAnchor(Vector newPos)
        {
            Vector oldPos = bungeeAnchor != null ? bungeeAnchor.pos : Vect(0f, 0f);
            float dx = newPos.X - oldPos.X;
            float dy = newPos.Y - oldPos.Y;

            if (parts != null)
            {
                foreach (ConstrainedPoint part in parts)
                {
                    part.pos = Vect(part.pos.X + dx, part.pos.Y + dy);

                    // Keep Verlet history aligned so teleport doesn't inject fake velocity.
                    if (part.prevPos.X != vectUndefined.X)
                    {
                        part.prevPos = Vect(part.prevPos.X + dx, part.prevPos.Y + dy);
                    }

                    // Only adjust pins that are actually set (unset pin is -1, -1).
                    if (part.pin.X != -1f || part.pin.Y != -1f)
                    {
                        part.pin = Vect(part.pin.X + dx, part.pin.Y + dy);
                    }
                }
            }
        }

        /// <summary>Number of constraint relaxation passes used by the bungee solver.</summary>
        public const int BUNGEE_RELAXION_TIMES = 30;

        /// <summary>Whether the bungee should be rendered with highlight brightness.</summary>
        public bool highlighted;

        /// <summary>Rest length used between adjacent bungee constraint points.</summary>
        public static float BUNGEE_REST_LEN = ActivePhysicsConstants.BungeeRestLength;

        /// <summary>Head anchor constraint point for the bungee.</summary>
        public ConstrainedPoint bungeeAnchor;

        /// <summary>Tail constraint point for the bungee.</summary>
        public ConstrainedPoint tail;

        /// <summary>Cut segment index, or <c>-1</c> when the bungee is uncut.</summary>
        public int cut;

        /// <summary>Current relaxation bucket derived from the bungee stretch distance.</summary>
        public int relaxed;

        /// <summary>Initial candle angle used by candle-related bungee behavior.</summary>
        public float initialCandleAngle;

        /// <summary>Whether this bungee is marked as the selected or active special instance.</summary>
        public bool chosenOne;

        /// <summary>Current bungee behavior mode.</summary>
        public int bungeeMode;

        /// <summary>Whether the bungee should render in white during a cut transition.</summary>
        public bool forceWhite;

        /// <summary>Remaining cut fade time in seconds.</summary>
        public float cutTime;

        /// <summary>
        /// Flat array of bezier curve points in the format [x0, y0, x1, y1, x2, y2, ...].
        /// Used for rendering the rope and positioning Christmas lights.
        /// </summary>
        public float[] drawPts = new float[ActivePhysicsConstants.DrawPtsBufferSize];

        /// <summary>
        /// Number of valid coordinates in the <see cref="drawPts" /> array.
        /// </summary>
        public int drawPtsCount;

        /// <summary>Base rendered line width for the bungee.</summary>
        public float lineWidth;

        /// <summary>Whether tail-side rope parts should be hidden after the bungee is cut.</summary>
        public bool hideTailParts;

        /// <summary>Whether this bungee renders as a chain.</summary>
        public bool breakable;

        /// <summary>Whether this chain can only be cut by an axe blade, not a finger trace or razor.</summary>
        public bool cutOnlyByAxe;

        /// <summary>Red channels of the three shades a masked chain link can take.</summary>
        private static readonly float[] ChainMaskRed = [0.78f, 0.85f, 0.88f];

        /// <summary>Green channels of the three shades a masked chain link can take.</summary>
        private static readonly float[] ChainMaskGreen = [0.71f, 0.83f, 0.85f];

        /// <summary>Blue channels of the three shades a masked chain link can take.</summary>
        private static readonly float[] ChainMaskBlue = [0.795f, 0.9f, 0.91f];

        /// <summary>Texture quad used at each sampled chain point.</summary>
        private const int ChainPointQuad = 0;

        /// <summary>Texture quad used between sampled chain points.</summary>
        private const int ChainMidpointQuad = 1;

        /// <summary>
        /// Bezier samples per segment for chain rendering. The original always passes 2 to
        /// <c>drawChain</c>, independent of the (higher) bungee rope sample density.
        /// </summary>
        private const int ChainDrawSamplePoints = 2;

        /// <summary>Backing seed for <see cref="ChainColorSeed" />; <c>null</c> until first use.</summary>
        private int? chainColorSeed;

        /// <summary>
        /// Stable per-bungee seed driving which chain links are masked. Generated once on first
        /// access (via the shared <see cref="MathHelper" /> RNG) so the masking pattern stays
        /// fixed for the rope's lifetime.
        /// </summary>
        private int ChainColorSeed => chainColorSeed ??= (int)Arc4random();

        /// <summary>
        /// A planned chain sprite draw.
        /// </summary>
        /// <param name="quadIndex">Texture quad index.</param>
        /// <param name="center">Sprite center.</param>
        /// <param name="rotation">Sprite rotation in radians.</param>
        /// <param name="vertexQuad">Rotated destination vertices.</param>
        internal readonly struct ChainSprite(int quadIndex, Vector center, float rotation, Quad3D vertexQuad)
        {
            /// <summary>Texture quad index.</summary>
            public int QuadIndex { get; } = quadIndex;

            /// <summary>Sprite center.</summary>
            public Vector Center { get; } = center;

            /// <summary>Sprite rotation in radians.</summary>
            public float Rotation { get; } = rotation;

            /// <summary>Rotated destination vertices.</summary>
            public Quad3D VertexQuad { get; } = vertexQuad;
        }

        /// <summary>Random number generator used for Christmas light frame selection.</summary>
        private static readonly Random christmasRandom = new();

        /// <summary>Seed captured on first Christmas light draw for reproducibility.</summary>
        private int? lightRandomSeed;

        /// <summary>
        /// Per-light frame indices, stored on first draw and reused for consistency.
        /// The number of lights can go up to 200.
        /// </summary>
        private readonly int[] lightFrames = new int[200];

        /// <summary>
        /// Number of lights determined on first draw (-1 = not yet determined).
        /// </summary>
        private int lightsCount = -1;

        /// <summary>Starting coordinate index for Christmas light placement in the current segment.</summary>
        private static int s_lightStartCoord;

        /// <summary>Number of coordinate indices to skip at the end of the current segment.</summary>
        private static int s_lightEndSkip;

        /// <summary>Running light index counter shared across head/tail draw calls.</summary>
        private static int s_lightCounter;

        /// <summary>Saved end overflow used to continue light placement into the tail segment.</summary>
        private static int s_lightSavedEnd;

        /// <summary>Whether this bungee owns and should dispose the anchor point.</summary>
        private bool ownsAnchor;

        /// <summary>Whether this bungee owns and should dispose the tail point.</summary>
        private bool ownsTail;

        /// <summary>Cached vertex array for main rope rendering.</summary>
        private static VertexPositionColor[] s_bungeeVerticesCache;

        /// <summary>Cached float array for outer glow vertex positions.</summary>
        private static float[] s_glowPositionsCache;

        /// <summary>Cached float array for inner rope vertex positions.</summary>
        private static float[] s_innerPositionsCache;

        /// <summary>Per-vertex color array for the outer glow triangle strip.</summary>
        private static readonly RGBAColor[] glowColors =
        [
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA
        ];

        /// <summary>Per-vertex color array for the inner rope triangle strip.</summary>
        private static readonly RGBAColor[] innerColors =
        [
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA,
            RGBAColor.transparentRGBA
        ];

        /// <summary>Default dark base color used when setting the renderer before drawing.</summary>
        private static Color s_ropeDrawColor = new(0f, 0f, 0.4f, 1f);
    }
}
