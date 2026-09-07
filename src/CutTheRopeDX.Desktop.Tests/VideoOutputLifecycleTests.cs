using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Framework.Platform;

using Xunit;

namespace CutTheRopeDX.Desktop.Tests
{
    /// <summary>
    /// Covers what a cutscene owes the game around it: it announces completion exactly once
    /// whether it ran out or was skipped, and it stops handing out frames once it is over.
    /// </summary>
    public sealed class VideoOutputLifecycleTests : IDisposable
    {
        private readonly Func<IVideoPlayer> previousFactory = PlatformServices.VideoPlayerFactory;
        private readonly FakeVideoPlayer player = new();

        public VideoOutputLifecycleTests()
        {
            PlatformServices.VideoPlayerFactory = () => player;
        }

        /// <summary>A video player whose playback the test drives directly.</summary>
        private sealed class FakeVideoPlayer : IVideoPlayer
        {
            private readonly FrameTexture frame = new();

            public List<string> Calls { get; } = [];
            public bool IsPaused { get; private set; }
            public bool Playing { get; private set; }
            public event Action PlaybackFinished;

            public void Play(string moviePath, bool mute)
            {
                Calls.Add($"play {moviePath} mute={mute}");
                Playing = true;
            }

            /// <summary>Ends playback the way running out of frames does.</summary>
            public void ReachEndOfMovie()
            {
                Playing = false;
                PlaybackFinished?.Invoke();
            }

            public ITextureHandle GetTexture()
            {
                return Playing ? frame : null;
            }

            public bool IsPlaying()
            {
                return Playing;
            }

            public bool IsTextureReady()
            {
                return Playing;
            }

            public void Stop()
            {
                Calls.Add("stop");
                Playing = false;
                PlaybackFinished?.Invoke();
            }

            public void Pause()
            {
                Calls.Add("pause");
                IsPaused = true;
            }

            public void Resume()
            {
                Calls.Add("resume");
                IsPaused = false;
            }

            public void Start()
            {
                Calls.Add("start");
            }

            public void Update()
            {
                Calls.Add("update");
            }

            public void Dispose()
            {
                Calls.Add("dispose");
            }

            private sealed class FrameTexture : ITextureHandle
            {
                public int Width => 1280;
                public int Height => 720;
                public void Dispose() { }
            }
        }

        /// <summary>Counts the completions a movie reports.</summary>
        private sealed class CompletionCounter : IMovieMgrDelegate
        {
            public int Count { get; private set; }
            public string LastUrl { get; private set; }

            public void MoviePlaybackFinished(string url)
            {
                Count++;
                LastUrl = url;
            }
        }

        private static (MovieMgr Movies, CompletionCounter Finished) StartMovie(bool mute = false)
        {
            MovieMgr movies = new();
            CompletionCounter finished = new();
            movies.delegateMovieMgrDelegate = finished;
            movies.PlayURL("ctr_intro", mute);
            movies.Start();
            return (movies, finished);
        }

        [Fact]
        public void AMovieThatRunsOutReportsCompletionExactlyOnce()
        {
            (MovieMgr movies, CompletionCounter finished) = StartMovie();

            player.ReachEndOfMovie();

            Assert.Equal(1, finished.Count);
            Assert.Equal("ctr_intro", finished.LastUrl);
            movies.Dispose();
        }

        [Fact]
        public void ASkippedMovieReportsCompletionExactlyOnce()
        {
            (MovieMgr movies, CompletionCounter finished) = StartMovie();

            movies.Stop();

            Assert.Equal(1, finished.Count);
            Assert.Contains("stop", player.Calls);
            movies.Dispose();
        }

        [Fact]
        public void SkippingAMovieThatAlreadyFinishedDoesNotReportItTwice()
        {
            // The pause menu and the back button both stop the movie, and a cutscene that ended on
            // its own must not be counted again when they do.
            (MovieMgr movies, CompletionCounter finished) = StartMovie();
            player.ReachEndOfMovie();

            movies.Stop();

            Assert.Equal(1, finished.Count);
            movies.Dispose();
        }

        [Fact]
        public void NoFrameIsHandedOutOnceTheMovieIsOver()
        {
            // A decoded frame left over from a stopped movie would be drawn over whatever screen
            // comes next, so the player stops offering one.
            (MovieMgr movies, _) = StartMovie();
            Assert.NotNull(movies.GetTexture());
            Assert.True(movies.IsTextureReady());

            movies.Stop();

            Assert.Null(movies.GetTexture());
            Assert.False(movies.IsTextureReady());
            Assert.False(movies.IsPlaying());
            movies.Dispose();
        }

        [Fact]
        public void PausingAndResumingReachThePlayer()
        {
            (MovieMgr movies, _) = StartMovie();

            movies.Pause();
            Assert.True(movies.IsPaused());

            movies.Resume();

            Assert.False(movies.IsPaused());
            Assert.Equal(["play ctr_intro mute=False", "start", "pause", "resume"], player.Calls);
            movies.Dispose();
        }

        [Fact]
        public void ASilencedMovieIsStartedMuted()
        {
            // Cutscenes are muted when the player has turned both music and effects off.
            (MovieMgr movies, _) = StartMovie(mute: true);

            Assert.Contains("play ctr_intro mute=True", player.Calls);
            movies.Dispose();
        }

        [Fact]
        public void DisposingTheMovieManagerReleasesItsPlayer()
        {
            (MovieMgr movies, CompletionCounter finished) = StartMovie();

            movies.Dispose();

            Assert.Contains("dispose", player.Calls);

            // The completion handler is detached with the player, so a late event from a disposed
            // player cannot reach a screen that has already moved on.
            player.ReachEndOfMovie();
            Assert.Equal(0, finished.Count);
        }

        public void Dispose()
        {
            PlatformServices.VideoPlayerFactory = previousFactory;
        }
    }
}
