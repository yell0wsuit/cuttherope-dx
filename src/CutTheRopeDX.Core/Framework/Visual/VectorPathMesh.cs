using System;
using System.Collections.Generic;
using System.Numerics;

using CutTheRopeDX.Framework.Core;

namespace CutTheRopeDX.Framework.Visual
{
    /// <summary>
    /// Contours resolved into drawable triangles under the nonzero winding rule. Building sweeps
    /// the whole shape, so a mesh is meant to be built once and then drawn many times under a
    /// transform rather than rebuilt per frame.
    /// </summary>
    internal sealed class VectorPathMesh
    {
        /// <summary>Heights closer together than this are treated as one sweep line.</summary>
        private const float BandEpsilon = 1e-5f;

        private VectorPathMesh(
            VertexPositionColor[] vertices, short[] indices, int indexCount)
        {
            Vertices = vertices;
            Indices = indices;
            IndexCount = indexCount;
        }

        /// <summary>Gets the mesh vertices, every one carrying the fill color.</summary>
        public VertexPositionColor[] Vertices { get; }

        /// <summary>Gets the triangle indices into <see cref="Vertices"/>.</summary>
        public short[] Indices { get; }

        /// <summary>Gets how many entries of <see cref="Indices"/> are in use.</summary>
        public int IndexCount { get; }

        /// <summary>
        /// Fills <paramref name="contours"/> under the nonzero winding rule.
        /// </summary>
        /// <param name="contours">Closed polylines, without repeated closing points.</param>
        /// <param name="fill">The color every vertex carries.</param>
        /// <returns>The built mesh.</returns>
        public static VectorPathMesh Build(
            IReadOnlyList<List<Vector2>> contours, RGBAColor fill)
        {
            Color color = fill.ToColor();
            List<Edge> edges = CollectEdges(contours);
            if (edges.Count == 0)
            {
                return new VectorPathMesh([], [], 0);
            }

            float[] bands = BandBoundaries(edges);
            List<VertexPositionColor> vertices = [];
            List<short> indices = [];
            List<Crossing> crossings = [];

            // A span stays open while the same two edges bound it, so one trapezoid can cover
            // many bands. The value is the height the run started at.
            Dictionary<(int Left, int Right), float> open = [];
            HashSet<(int Left, int Right)> current = [];
            List<(int Left, int Right)> closed = [];

            for (int band = 0; band + 1 < bands.Length; band++)
            {
                float top = bands[band];
                float bottom = bands[band + 1];
                if (bottom - top <= BandEpsilon)
                {
                    continue;
                }

                // Sorting at the band's middle is what makes the order unambiguous: no edge
                // crosses another inside a band, so the order there holds across the whole band.
                float middle = (top + bottom) * 0.5f;
                crossings.Clear();
                for (int i = 0; i < edges.Count; i++)
                {
                    Edge edge = edges[i];
                    if (edge.Top > top + BandEpsilon || edge.Bottom < bottom - BandEpsilon)
                    {
                        continue;
                    }
                    crossings.Add(new Crossing(edge.XAt(middle), i, edge.Direction));
                }
                crossings.Sort(static (a, b) => a.MiddleX.CompareTo(b.MiddleX));

                current.Clear();
                int winding = 0;
                for (int i = 0; i + 1 < crossings.Count; i++)
                {
                    winding += crossings[i].Direction;
                    if (winding != 0)
                    {
                        current.Add((crossings[i].EdgeIndex, crossings[i + 1].EdgeIndex));
                    }
                }

                closed.Clear();
                foreach ((int Left, int Right) span in open.Keys)
                {
                    if (!current.Contains(span))
                    {
                        closed.Add(span);
                    }
                }
                foreach ((int Left, int Right) span in closed)
                {
                    AppendTrapezoid(vertices, indices, color, edges, span, open[span], top);
                    open.Remove(span);
                }

                foreach ((int Left, int Right) span in current)
                {
                    if (!open.ContainsKey(span))
                    {
                        open[span] = top;
                    }
                }
            }

            float lastBand = bands[^1];
            foreach (KeyValuePair<(int Left, int Right), float> span in open)
            {
                AppendTrapezoid(
                    vertices, indices, color, edges, span.Key, span.Value, lastBand);
            }

            return new VectorPathMesh([.. vertices], [.. indices], indices.Count);
        }

        private static void AppendTrapezoid(
            List<VertexPositionColor> vertices,
            List<short> indices,
            Color color,
            List<Edge> edges,
            (int Left, int Right) span,
            float top,
            float bottom)
        {
            if (bottom - top <= BandEpsilon)
            {
                return;
            }

            Edge left = edges[span.Left];
            Edge right = edges[span.Right];
            float topLeft = left.XAt(top);
            float topRight = right.XAt(top);
            float bottomLeft = left.XAt(bottom);
            float bottomRight = right.XAt(bottom);
            if (topLeft >= topRight && bottomLeft >= bottomRight)
            {
                return;
            }
            if (vertices.Count + 4 > short.MaxValue)
            {
                throw new InvalidOperationException(
                    "The filled shape does not fit a short index buffer.");
            }

            short baseIndex = (short)vertices.Count;
            vertices.Add(new VertexPositionColor(new Vector3(topLeft, top, 0f), color));
            vertices.Add(new VertexPositionColor(new Vector3(topRight, top, 0f), color));
            vertices.Add(new VertexPositionColor(new Vector3(bottomLeft, bottom, 0f), color));
            vertices.Add(new VertexPositionColor(new Vector3(bottomRight, bottom, 0f), color));

            indices.Add(baseIndex);
            indices.Add((short)(baseIndex + 1));
            indices.Add((short)(baseIndex + 2));
            indices.Add((short)(baseIndex + 1));
            indices.Add((short)(baseIndex + 3));
            indices.Add((short)(baseIndex + 2));
        }

        private static List<Edge> CollectEdges(IReadOnlyList<List<Vector2>> contours)
        {
            List<Edge> edges = [];
            foreach (List<Vector2> contour in contours)
            {
                if (contour == null || contour.Count < 3)
                {
                    continue;
                }
                for (int i = 0; i < contour.Count; i++)
                {
                    Vector2 start = contour[i];
                    Vector2 end = contour[(i + 1) % contour.Count];
                    if (MathF.Abs(end.Y - start.Y) <= BandEpsilon)
                    {
                        // A horizontal edge crosses no sweep line, so it contributes no winding.
                        continue;
                    }
                    edges.Add(new Edge(start, end));
                }
            }
            return edges;
        }

        private static float[] BandBoundaries(List<Edge> edges)
        {
            List<float> heights = new(edges.Count * 2);
            foreach (Edge edge in edges)
            {
                heights.Add(edge.Top);
                heights.Add(edge.Bottom);
            }
            heights.Sort();

            List<float> distinct = new(heights.Count) { heights[0] };
            for (int i = 1; i < heights.Count; i++)
            {
                if (heights[i] - distinct[^1] > BandEpsilon)
                {
                    distinct.Add(heights[i]);
                }
            }
            return [.. distinct];
        }

        /// <summary>One non-horizontal contour edge, with the direction it crosses a sweep line.</summary>
        private readonly struct Edge
        {
            private readonly float x0;
            private readonly float y0;
            private readonly float slope;

            public Edge(Vector2 start, Vector2 end)
            {
                x0 = start.X;
                y0 = start.Y;
                slope = (end.X - start.X) / (end.Y - start.Y);
                Direction = end.Y > start.Y ? 1 : -1;
                Top = MathF.Min(start.Y, end.Y);
                Bottom = MathF.Max(start.Y, end.Y);
            }

            /// <summary>Gets +1 when the edge runs downward, -1 when upward.</summary>
            public int Direction { get; }

            /// <summary>Gets the edge's smallest Y.</summary>
            public float Top { get; }

            /// <summary>Gets the edge's largest Y.</summary>
            public float Bottom { get; }

            /// <summary>Returns where the edge sits at a height.</summary>
            /// <param name="y">Height to sample.</param>
            /// <returns>The edge's X at that height.</returns>
            public float XAt(float y)
            {
                return x0 + ((y - y0) * slope);
            }
        }

        private readonly struct Crossing(float middleX, int edgeIndex, int direction)
        {
            public readonly float MiddleX = middleX;
            public readonly int EdgeIndex = edgeIndex;
            public readonly int Direction = direction;
        }
    }
}
