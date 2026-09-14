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
    /// Om Nom filling the screen as vector artwork when the player taps him. The layer meshes are
    /// tessellated once and redrawn under a transform; only the antialiasing band is rebuilt, and
    /// only when the scale or the fade moves, so that it keeps a constant width on screen.
    /// </summary>
    internal sealed class EasterEggOmNom
    {
        /// <summary>How wide the antialiasing band is on screen, in design units.</summary>
        private const float FringeWidth = 2.5f;

        /// <summary>Curve flattening accuracy, in path units.</summary>
        private const float FlattenTolerance = 0.1f;

        /// <summary>Index of the first pupil layer; the pupils are the last two layers.</summary>
        private const int FirstPupilLayer = 4;

        private readonly EasterEggOmNomAnimation animation = new();
        private readonly List<VectorPathMesh> meshes = [];
        private readonly List<VertexPositionColor[]> tintedMeshes = [];
        private readonly List<List<List<Vector2>>> contours = [];
        private readonly List<VectorFringe> fringes = [];

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

        /// <summary>Advances the animation.</summary>
        /// <param name="deltaSeconds">Seconds since the previous update.</param>
        public void Update(float deltaSeconds)
        {
            animation.Update(deltaSeconds);
        }

        /// <summary>Draws the current frame in screen space.</summary>
        public void Draw()
        {
            if (!animation.IsActive)
            {
                return;
            }

            EasterEggOmNomFrame frame = animation.CurrentFrame;
            if (frame.Alpha <= 0f || meshes.Count == 0)
            {
                return;
            }

            Renderer.Enable(Renderer.GL_BLEND);
            Renderer.Disable(Renderer.GL_TEXTURE_2D);
            Renderer.SetBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
            Renderer.SetColor(Color.White);

            Renderer.PushMatrix();
            Renderer.Translate(frame.X, frame.Y, 0f);
            Renderer.Scale(frame.ScaleX, frame.ScaleY, 1f);

            // The band is authored in path units but has to land at a fixed width on screen, so
            // it shrinks as the transform above grows.
            float fringeWidth = FringeWidth / MathF.Max(frame.ScaleX, 0.0001f);

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
            Renderer.Enable(Renderer.GL_TEXTURE_2D);
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
