using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

using CutTheRopeDX.Content.Assets;

using Xunit;

namespace CutTheRopeDX.Content.Tests
{
    /// <summary>
    /// A download that stops receiving data fails in seconds, wherever it stops.
    /// </summary>
    /// <remarks>
    /// The fetch runs inside the game's build, where the person building sees only a target name
    /// and a timer. A connection that goes quiet has to end in an error they can read, not in a
    /// build that looks hung.
    /// </remarks>
    public sealed class AssetDownloaderTests : IDisposable
    {
        private static readonly TimeSpan StallTimeout = TimeSpan.FromMilliseconds(300);

        private readonly string destination = Path.Combine(
            Path.GetTempPath(), $"ctrdx-download-{Guid.NewGuid():N}.zip");

        public void Dispose()
        {
            File.Delete(destination);
        }

        [Fact]
        public async Task FailsWhenTheServerNeverResponds()
        {
            await AssertStallFailsAsync(async (stream, token) =>
                await Task.Delay(Timeout.Infinite, token));
        }

        [Fact]
        public async Task FailsWhenTheBodyStopsPartWay()
        {
            await AssertStallFailsAsync(async (stream, token) =>
            {
                byte[] headers = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\nContent-Length: 1000000\r\n\r\n");
                await stream.WriteAsync(headers, token);
                await stream.WriteAsync(new byte[1000], token);
                await stream.FlushAsync(token);
                await Task.Delay(Timeout.Infinite, token);
            });
        }

        [Fact]
        public async Task WritesACompleteBody()
        {
            byte[] body = Encoding.ASCII.GetBytes("assets");
            await using StallingServer server = new(async (stream, token) =>
            {
                byte[] headers = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(headers, token);
                await stream.WriteAsync(body, token);
            });
            using AssetDownloader downloader = new(new HttpClient(), StallTimeout);

            await downloader.DownloadAsync(
                server.Url, destination, retries: 1, TestContext.Current.CancellationToken);

            Assert.Equal(
                body,
                await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
        }

        private async Task AssertStallFailsAsync(Func<NetworkStream, CancellationToken, Task> respond)
        {
            await using StallingServer server = new(respond);
            using AssetDownloader downloader = new(new HttpClient(), StallTimeout);
            Stopwatch elapsed = Stopwatch.StartNew();

            _ = await Assert.ThrowsAsync<TimeoutException>(
                () => downloader.DownloadAsync(
                    server.Url, destination, retries: 1, TestContext.Current.CancellationToken));

            Assert.True(
                elapsed.Elapsed < TimeSpan.FromSeconds(10),
                $"The stalled download took {elapsed.Elapsed} to fail.");
        }

        private sealed class StallingServer : IAsyncDisposable
        {
            private readonly TcpListener listener = new(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource stop = new();
            private readonly Task serving;

            public StallingServer(Func<NetworkStream, CancellationToken, Task> respond)
            {
                listener.Start();
                Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/assets.zip";
                serving = ServeAsync(respond);
            }

            public string Url { get; }

            public async ValueTask DisposeAsync()
            {
                await stop.CancelAsync();
                listener.Stop();
                try
                {
                    await serving;
                }
                catch (Exception exception) when (
                    exception is OperationCanceledException or SocketException or IOException)
                {
                    // The server is only ever stopped mid-response.
                }
                stop.Dispose();
            }

            private async Task ServeAsync(Func<NetworkStream, CancellationToken, Task> respond)
            {
                using TcpClient client = await listener.AcceptTcpClientAsync(stop.Token);
                await using NetworkStream stream = client.GetStream();
                byte[] request = new byte[4096];
                _ = await stream.ReadAsync(request, stop.Token);
                await respond(stream, stop.Token);
            }
        }
    }
}
