using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// A voice is built per play and never handed out twice, so whoever drops it is the last one
    /// who could have released it. The backend behind these holds a native mixer track and a
    /// properties object per voice and has no finalizer to fall back on, so an instance that
    /// leaves its tracking list undisposed is leaked for the life of the process.
    /// </summary>
    public sealed class SoundMgrInstanceReleaseTests
    {
        [Fact]
        public void AVoiceThatFinishedOnItsOwnIsReleasedWhenTheNextPlayTidiesUp()
        {
            using Harness harness = new();

            TrackedInstance first = harness.PlayOneShot();
            first.Finish();
            _ = harness.PlayOneShot();

            Assert.True(first.Disposed);
        }

        [Fact]
        public void StoppingEverythingReleasesTheVoicesItStopped()
        {
            using Harness harness = new();

            TrackedInstance oneShot = harness.PlayOneShot();
            TrackedInstance looped = harness.PlayLooped();

            harness.Manager.StopAllSounds();

            Assert.True(oneShot.Disposed);
            Assert.True(looped.Disposed);
        }

        [Fact]
        public void StoppingASingleTrackedVoiceReleasesIt()
        {
            using Harness harness = new();

            TrackedInstance oneShot = harness.PlayOneShot();
            TrackedInstance looped = harness.PlayLooped();

            harness.Manager.StopSound(oneShot);
            harness.Manager.StopLoopedSound(looped);

            Assert.True(oneShot.Disposed);
            Assert.True(looped.Disposed);
        }

        [Fact]
        public void RepeatedOneShotsDoNotAccumulateUndisposedVoices()
        {
            using Harness harness = new();

            for (int play = 0; play < 200; play++)
            {
                harness.PlayOneShot().Finish();
            }

            // The last one is still tracked: nothing has tidied up after it yet.
            Assert.Equal(1, harness.Backend.LiveInstances);
        }

        private sealed class Harness : IDisposable
        {
            private static readonly string[] Used = [Resources.Snd.Bouncer, Resources.Snd.Electric];

            public Harness()
            {
                _ = HeadlessGame.Boot();
                Manager = Application.SharedSoundMgr();
                Backend = new TrackingBackend();
                SoundMgr.SetBackend(Backend);

                // The manager is a process-wide singleton and swapping the backend does not empty
                // its effect cache, so anything a previous test loaded would still be handed out
                // here - and its voices would be counted against that test's backend, not this one.
                Free();
            }

            public SoundMgr Manager { get; }

            public TrackingBackend Backend { get; }

            public TrackedInstance PlayOneShot()
            {
                return (TrackedInstance)Manager.PlaySoundTracked(Resources.Snd.Bouncer);
            }

            public TrackedInstance PlayLooped()
            {
                return (TrackedInstance)Manager.PlaySoundLooped(Resources.Snd.Electric);
            }

            public void Dispose()
            {
                Manager.StopAllSounds();
                Free();
                SoundMgr.SetBackend(null);
            }

            private void Free()
            {
                foreach (string resource in Used)
                {
                    Manager.FreeSound(resource);
                }
            }
        }

        private sealed class TrackingBackend : IAudioBackend
        {
            private readonly List<TrackedInstance> created = [];

            public int LiveInstances
            {
                get
                {
                    int live = 0;
                    foreach (TrackedInstance instance in created)
                    {
                        if (!instance.Disposed)
                        {
                            live++;
                        }
                    }
                    return live;
                }
            }

            public ISoundEffect LoadSound(string contentPath)
            {
                return new TrackedEffect(created);
            }

            public IMusicTrack LoadMusic(string contentPath)
            {
                throw new NotSupportedException();
            }

            public void PlayMusic(IMusicTrack track, bool repeating)
            {
            }

            public void StopMusic()
            {
            }

            public void PauseMusic()
            {
            }

            public void ResumeMusic()
            {
            }

            public AudioPlaybackState MusicState => AudioPlaybackState.Stopped;
        }

        private sealed class TrackedEffect(List<TrackedInstance> created) : ISoundEffect
        {
            public ISoundInstance CreateInstance()
            {
                TrackedInstance instance = new();
                created.Add(instance);
                return instance;
            }

            public void Dispose()
            {
            }
        }

        private sealed class TrackedInstance : ISoundInstance
        {
            public bool IsLooped { get; set; }

            public float Volume { get; set; } = 1f;

            public AudioPlaybackState State { get; private set; } = AudioPlaybackState.Stopped;

            public bool Disposed { get; private set; }

            /// <summary>Ends playback the way a one-shot does when it reaches its last sample.</summary>
            public void Finish()
            {
                State = AudioPlaybackState.Stopped;
            }

            public void Play()
            {
                State = AudioPlaybackState.Playing;
            }

            public void Stop()
            {
                State = AudioPlaybackState.Stopped;
            }

            public void Pause()
            {
                State = AudioPlaybackState.Paused;
            }

            public void Resume()
            {
                State = AudioPlaybackState.Playing;
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
