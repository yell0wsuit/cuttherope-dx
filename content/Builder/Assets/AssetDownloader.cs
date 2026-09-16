namespace CutTheRopeDX.Content.Assets
{
    /// <summary>
    /// Downloads the binary asset archive with bounded retries.
    /// </summary>
    public sealed class AssetDownloader : IDisposable
    {
        private static readonly TimeSpan DefaultStallTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly TimeSpan _stallTimeout;

        /// <summary>
        /// Initializes a downloader with the default HTTP client.
        /// </summary>
        public AssetDownloader()
        {
            // No limit on the whole request: on a slow connection the archive can rightly take
            // longer than any fixed figure. Waiting is bounded per step by the stall timeout.
            _httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "CutTheRopeDX.Content/1.0");
            _ownsHttpClient = true;
            _stallTimeout = DefaultStallTimeout;
        }

        /// <summary>
        /// Initializes a downloader with a caller-provided HTTP client.
        /// </summary>
        /// <param name="httpClient">HTTP client used for requests.</param>
        /// <param name="stallTimeout">How long to wait for a response, or for more of the body, before giving up.</param>
        public AssetDownloader(HttpClient httpClient, TimeSpan stallTimeout)
        {
            _httpClient = httpClient;
            _stallTimeout = stallTimeout;
        }

        /// <summary>
        /// Downloads an archive to disk.
        /// </summary>
        /// <param name="url">Archive URL.</param>
        /// <param name="destinationPath">Destination file path.</param>
        /// <param name="retries">Maximum request attempts.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="TimeoutException">Nothing arrived within the stall timeout.</exception>
        public async Task DownloadAsync(
            string url,
            string destinationPath,
            int retries,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(retries, 1);

            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await DownloadOnceAsync(url, destinationPath, cancellationToken);
                    return;
                }
                catch (Exception exception) when (
                    attempt < retries &&
                    exception is HttpRequestException or IOException or TimeoutException)
                {
                    Console.Error.WriteLine(
                        $"Download attempt {attempt} failed ({exception.Message}); retrying...");
                    await Task.Delay(RetryDelay, cancellationToken);
                }
            }
        }

        private async Task DownloadOnceAsync(
            string url,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            // Restarted before every wait, so it fires on silence rather than on total duration:
            // an unreachable host, a server that never answers and a connection that dies
            // part-way through all end the same way.
            using CancellationTokenSource stall =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                stall.CancelAfter(_stallTimeout);
                using HttpResponseMessage response = await _httpClient.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    stall.Token);
                _ = response.EnsureSuccessStatusCode();
                await using Stream source =
                    await response.Content.ReadAsStreamAsync(stall.Token);
                await using FileStream destination = File.Create(destinationPath);

                byte[] buffer = new byte[81920];
                while (true)
                {
                    stall.CancelAfter(_stallTimeout);
                    int read = await source.ReadAsync(buffer, stall.Token);
                    if (read == 0)
                    {
                        return;
                    }
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"nothing was received for {_stallTimeout.TotalSeconds:0.#} seconds");
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }
    }
}
