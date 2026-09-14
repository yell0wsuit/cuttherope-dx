using System;
using System.Collections.Generic;
using System.Numerics;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Om Nom filling the screen as vector artwork over a dimmed level when the player taps him.
    /// The layer meshes are tessellated once and redrawn under a transform; only the antialiasing
    /// band is rebuilt, and only when the scale or the fade moves, so that it keeps a constant
    /// width on screen.
    /// </summary>
    internal sealed class EasterEggOmNom
    {
        /// <summary>How wide the antialiasing band is on screen, in design units.</summary>
        private const float FringeWidth = 2.5f;

        /// <summary>Curve flattening accuracy, in path units.</summary>
        private const float FlattenTolerance = 0.1f;

        /// <summary>Index of the first pupil layer; the pupils are the last two layers.</summary>
        private const int FirstPupilLayer = 4;

        /// <summary>Opacity of the black dim behind Om Nom when the overlay is fully up.</summary>
        private const float DimOpacity = 0.6f;

        private static readonly short[] DimIndices = [0, 1, 2, 1, 3, 2];

        private readonly EasterEggOmNomAnimation animation = new();
        private readonly List<VectorPathMesh> meshes = [];
        private readonly List<VertexPositionColor[]> tintedMeshes = [];
        private readonly List<List<List<Vector2>>> contours = [];
        private readonly List<VectorFringe> fringes = [];
        private readonly VertexPositionColor[] dimVertices = new VertexPositionColor[4];

        private float builtFringeWidth = float.NaN;
        private float builtFringeAlpha = float.NaN;

        /// <summary>Gets a value indicating whether the egg still has something to draw.</summary>
        public bool IsActive => animation.IsActive;

        /// <summary>Gets a value indicating whether the level should stay frozen.</summary>
        public bool FreezesGameplay => animation.FreezesGameplay;

        /// <summary>Starts the animation from the beginning.</summary>
        public void Trigger()
        {
            EnsureMeshes();
            builtFringeWidth = float.NaN;
            builtFringeAlpha = float.NaN;
            animation.Start();
        }

        /// <summary>Fades the overlay out early, leaving Om Nom where he is.</summary>
        /// <returns><see langword="true"/> when this call started the dismissal.</returns>
        public bool Cancel()
        {
            return animation.Cancel();
        }

        /// <summary>Removes the egg at once, with no closing fade.</summary>
        public void Clear()
        {
            animation.Stop();
        }

        /// <summary>Advances the animation.</summary>
        /// <param name="deltaSeconds">Seconds since the previous update.</param>
        public void Update(float deltaSeconds)
        {
            animation.Update(deltaSeconds);
        }

        /// <summary>Draws the current frame in screen space.</summary>
        /// <param name="screen">The visible screen region the dim covers.</param>
        public void Draw(CTRRectangle screen)
        {
            if (!animation.IsActive)
            {
                return;
            }

            EasterEggOmNomFrame frame = animation.CurrentFrame;
            if (frame.Alpha <= 0f)
            {
                return;
            }

            Renderer.Enable(Renderer.GL_BLEND);
            Renderer.Disable(Renderer.GL_TEXTURE_2D);
            Renderer.SetBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
            Renderer.SetColor(Color.White);

            DrawDim(screen, frame.Alpha);
            if (frame.ShowsOmNom && meshes.Count > 0)
            {
                DrawOmNom(frame, Composition(screen));
            }

            Renderer.Enable(Renderer.GL_TEXTURE_2D);
        }

        private void DrawDim(CTRRectangle screen, float alpha)
        {
            Color color = RGBAColor.MakeRGBA(0f, 0f, 0f, DimOpacity * alpha).ToColor();
            float right = screen.x + screen.w;
            float bottom = screen.y + screen.h;
            dimVertices[0] = new VertexPositionColor(new Vector3(screen.x, screen.y, 0f), color);
            dimVertices[1] = new VertexPositionColor(new Vector3(right, screen.y, 0f), color);
            dimVertices[2] = new VertexPositionColor(new Vector3(screen.x, bottom, 0f), color);
            dimVertices[3] = new VertexPositionColor(new Vector3(right, bottom, 0f), color);
            Renderer.DrawTriangleList(dimVertices, DimIndices, DimIndices.Length);
        }

        /// <summary>
        /// Places the design-space composition in a viewport: scaled to fit, centered across, and
        /// resting on the bottom edge, so he rises from the bottom of any window shape. A 16:9
        /// viewport the size of the design box places it untouched.
        /// </summary>
        /// <param name="screen">The visible screen region.</param>
        /// <returns>Where the composition's origin lands, and the uniform scale it is drawn at.</returns>
        internal static (float X, float Y, float Scale) Composition(CTRRectangle screen)
        {
            float scale = LayoutMath.Contain(
                ViewportLayout.DesignWidth, ViewportLayout.DesignHeight, screen);
            float width = ViewportLayout.DesignWidth * scale;
            float height = ViewportLayout.DesignHeight * scale;
            return (screen.x + ((screen.w - width) / 2f), screen.y + screen.h - height, scale);
        }

        private void DrawOmNom(EasterEggOmNomFrame frame, (float X, float Y, float Scale) composition)
        {
            Renderer.PushMatrix();
            Renderer.Translate(composition.X, composition.Y, 0f);
            Renderer.Scale(composition.Scale, composition.Scale, 1f);
            Renderer.Translate(frame.X, frame.Y, 0f);
            Renderer.Scale(frame.ScaleX, frame.ScaleY, 1f);

            // The band is authored in path units but has to land at a fixed width on screen, so
            // it shrinks as the transforms above grow.
            float fringeWidth = FringeWidth / MathF.Max(frame.ScaleX * composition.Scale, 0.0001f);

            // Neither the width nor the color moves while he is held at full size, which is most
            // of the animation, and the band is identical frame to frame across that stretch.
            bool rebuild = fringeWidth != builtFringeWidth || frame.Alpha != builtFringeAlpha;
            builtFringeWidth = fringeWidth;
            builtFringeAlpha = frame.Alpha;

            for (int i = 0; i < meshes.Count; i++)
            {
                VectorPathMesh mesh = meshes[i];
                RGBAColor fill = Faded(EasterEggOmNomArt.Layers[i].Fill, frame.Alpha);

                Renderer.PushMatrix();
                if (i >= FirstPupilLayer)
                {
                    // The pupils slide within the face, so their offset is in path units and
                    // rides the same scale as the rest of him.
                    Renderer.Translate(frame.EyeOffset, 0f, 0f);
                }

                if (rebuild)
                {
                    Tint(mesh, tintedMeshes[i], fill);
                    fringes[i].Rebuild(contours[i], fill, fringeWidth);
                }
                Renderer.DrawTriangleList(tintedMeshes[i], mesh.Indices, mesh.IndexCount);
                Renderer.DrawTriangleList(
                    fringes[i].Vertices, fringes[i].Indices, fringes[i].IndexCount);

                Renderer.PopMatrix();
            }

            Renderer.PopMatrix();
        }

        private static void Tint(VectorPathMesh mesh, VertexPositionColor[] target, RGBAColor fill)
        {
            Color color = fill.ToColor();
            VertexPositionColor[] vertices = mesh.Vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                target[i] = new VertexPositionColor(vertices[i].Position, color);
            }
        }

        /// <summary>
        /// Scales a color into a faded, premultiplied form. Both the channels and the alpha move,
        /// because the blend the renderer runs consumes premultiplied source colors.
        /// </summary>
        private static RGBAColor Faded(RGBAColor color, float alpha)
        {
            return RGBAColor.MakeRGBA(
                color.RedColor * alpha,
                color.GreenColor * alpha,
                color.BlueColor * alpha,
                color.AlphaChannel * alpha);
        }

        private void EnsureMeshes()
        {
            if (meshes.Count > 0)
            {
                return;
            }

            foreach (EasterEggOmNomArt.Layer layer in EasterEggOmNomArt.Layers)
            {
                List<List<Vector2>> flattened =
                    VectorPath.Flatten(layer.Commands, FlattenTolerance);
                // The contours are kept because the fringe is rebuilt from them; the fill mesh
                // is built from them once and never again.
                contours.Add(flattened);
                VectorPathMesh mesh = VectorPathMesh.Build(flattened, layer.Fill);
                meshes.Add(mesh);
                tintedMeshes.Add(new VertexPositionColor[mesh.Vertices.Length]);
                fringes.Add(new VectorFringe());
            }
        }
    }
}
