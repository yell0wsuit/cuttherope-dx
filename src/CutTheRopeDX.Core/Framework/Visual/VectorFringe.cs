using System;
using System.Collections.Generic;
using System.Numerics;

using CutTheRopeDX.Framework.Core;

namespace CutTheRopeDX.Framework.Visual
{
    /// <summary>
    /// A band of triangles hugging the outside of a filled region, fading from the fill color to
    /// transparent. It stands in for the coverage antialiasing untextured triangles do not get,
    /// and is rebuilt whenever the shape's on-screen scale changes so the band keeps a constant
    /// width in pixels rather than in the shape's own units.
    /// </summary>
    internal sealed class VectorFringe
    {
        /// <summary>How far off an edge the inside/outside probe steps, in path units.</summary>
        private const float ProbeDistance = 1e-3f;

        private VertexPositionColor[] vertices = [];
        private short[] indices = [];

        /// <summary>Gets the band's vertices.</summary>
        public VertexPositionColor[] Vertices => vertices;

        /// <summary>Gets the band's triangle indices.</summary>
        public short[] Indices => indices;

        /// <summary>Gets how many entries of <see cref="Indices"/> are in use.</summary>
        public int IndexCount { get; private set; }

        /// <summary>
        /// Rebuilds the band around whichever of <paramref name="contours"/> bound the filled
        /// region.
        /// </summary>
        /// <param name="contours">The same closed contours the fill was built from.</param>
        /// <param name="fill">The color the band fades from.</param>
        /// <param name="width">Band width, in the same units as the contours.</param>
        public void Rebuild(
            IReadOnlyList<List<Vector2>> contours, RGBAColor fill, float width)
        {
            int edgeCount = 0;
            foreach (List<Vector2> boundary in contours)
            {
                if (boundary.Count >= 3)
                {
                    edgeCount += boundary.Count;
                }
            }

            EnsureCapacity(edgeCount);
            IndexCount = 0;

            Color opaque = fill.ToColor();
            Color transparent = RGBAColor.transparentRGBA.ToColor();
            int vertexIndex = 0;

            foreach (List<Vector2> boundary in contours)
            {
                if (boundary.Count < 3)
                {
                    continue;
                }

                // Orientation is uniform along a contour, so which side is empty only has to be
                // established once per contour rather than per edge. A contour with the same
                // answer on both sides sits inside the filled region and edges nothing.
                if (!TryOutwardSign(contours, boundary, out float outwardSign))
                {
                    continue;
                }

                for (int i = 0; i < boundary.Count; i++)
                {
                    Vector2 start = boundary[i];
                    Vector2 end = boundary[(i + 1) % boundary.Count];
                    Vector2 normal = Normal(start, end) * outwardSign;

                    Vector2 startOuter = start + (normal * width);
                    Vector2 endOuter = end + (normal * width);

                    short baseIndex = (short)vertexIndex;
                    vertices[vertexIndex++] = Vertex(start, opaque);
                    vertices[vertexIndex++] = Vertex(end, opaque);
                    vertices[vertexIndex++] = Vertex(startOuter, transparent);
                    vertices[vertexIndex++] = Vertex(endOuter, transparent);

                    indices[IndexCount++] = baseIndex;
                    indices[IndexCount++] = (short)(baseIndex + 1);
                    indices[IndexCount++] = (short)(baseIndex + 2);
                    indices[IndexCount++] = (short)(baseIndex + 1);
                    indices[IndexCount++] = (short)(baseIndex + 3);
                    indices[IndexCount++] = (short)(baseIndex + 2);
                }
            }
        }

        private static VertexPositionColor Vertex(Vector2 position, Color color)
        {
            return new VertexPositionColor(new Vector3(position.X, position.Y, 0f), color);
        }

        private static Vector2 Normal(Vector2 start, Vector2 end)
        {
            Vector2 direction = end - start;
            float length = direction.Length();
            if (length <= float.Epsilon)
            {
                return Vector2.Zero;
            }
            direction /= length;
            return new Vector2(direction.Y, -direction.X);
        }

        /// <summary>
        /// Determines which way a contour's edge normals point away from the filled region.
        /// Probing the winding number on both sides settles it without assuming anything about
        /// how the contours happen to be wound.
        /// </summary>
        /// <param name="contours">Every contour, since winding is a property of all of them.</param>
        /// <param name="boundary">The contour being classified.</param>
        /// <param name="sign">+1 when the normal points away from the fill, -1 when into it.</param>
        /// <returns>
        /// <see langword="true"/> when the contour separates filled from empty; otherwise,
        /// <see langword="false"/>, meaning it lies wholly inside or outside and edges nothing.
        /// </returns>
        private static bool TryOutwardSign(
            IReadOnlyList<List<Vector2>> contours, List<Vector2> boundary, out float sign)
        {
            Vector2 start = boundary[0];
            Vector2 end = boundary[1];
            Vector2 midpoint = (start + end) * 0.5f;
            Vector2 normal = Normal(start, end);

            bool positiveFilled = IsInside(contours, midpoint + (normal * ProbeDistance));
            bool negativeFilled = IsInside(contours, midpoint - (normal * ProbeDistance));

            if (positiveFilled == negativeFilled)
            {
                sign = 0f;
                return false;
            }

            sign = positiveFilled ? -1f : 1f;
            return true;
        }

        private static bool IsInside(IReadOnlyList<List<Vector2>> contours, Vector2 point)
        {
            int winding = 0;
            foreach (List<Vector2> boundary in contours)
            {
                for (int i = 0; i < boundary.Count; i++)
                {
                    Vector2 start = boundary[i];
                    Vector2 end = boundary[(i + 1) % boundary.Count];
                    if (start.Y <= point.Y)
                    {
                        if (end.Y > point.Y && IsLeft(start, end, point) > 0f)
                        {
                            winding++;
                        }
                    }
                    else if (end.Y <= point.Y && IsLeft(start, end, point) < 0f)
                    {
                        winding--;
                    }
                }
            }
            return winding != 0;
        }

        private static float IsLeft(Vector2 start, Vector2 end, Vector2 point)
        {
            return ((end.X - start.X) * (point.Y - start.Y))
                - ((point.X - start.X) * (end.Y - start.Y));
        }

        private void EnsureCapacity(int edgeCount)
        {
            int neededVertices = edgeCount * 4;
            int neededIndices = edgeCount * 6;
            if (neededVertices > short.MaxValue)
            {
                throw new InvalidOperationException(
                    $"A fringe of {edgeCount} edges does not fit a short index buffer.");
            }
            if (vertices.Length < neededVertices)
            {
                vertices = new VertexPositionColor[neededVertices];
            }
            if (indices.Length < neededIndices)
            {
                indices = new short[neededIndices];
            }
        }
    }
}
