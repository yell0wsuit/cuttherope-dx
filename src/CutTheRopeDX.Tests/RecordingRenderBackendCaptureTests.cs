using System.Numerics;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>Covers the vertex capture the geometry tests assert against.</summary>
    public sealed class RecordingRenderBackendCaptureTests
    {
        [Fact]
        public void CapturesTriangleListsDereferencedInIndexOrder()
        {
            RecordingRenderBackend renderer = new();
            PlatformServices.Render = renderer;

            try
            {
                VertexPositionColor[] vertices =
                [
                    new(new Vector3(0f, 0f, 0f), Color.White),
                    new(new Vector3(1f, 0f, 0f), Color.White),
                    new(new Vector3(0f, 1f, 0f), Color.White),
                ];
                short[] indices = [0, 1, 2, 2, 1, 0];

                Renderer.DrawTriangleList(vertices, indices, 6);

                Assert.Single(renderer.CapturedLists);
                Assert.Equal(6, renderer.CapturedLists[0].Length);
                Assert.Equal(0f, renderer.CapturedLists[0][0].Position.X);
                Assert.Equal(0f, renderer.CapturedLists[0][5].Position.X);
                Assert.Equal(1f, renderer.CapturedLists[0][4].Position.X);
            }
            finally
            {
                PlatformServices.Render = new ThrowingRenderBackend();
            }
        }
    }
}
