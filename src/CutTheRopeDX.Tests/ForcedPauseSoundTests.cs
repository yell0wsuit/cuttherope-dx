using System;
using System.Collections.Generic;
using System.IO;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// A pause the player asked for answers with the button tap; one the host forces, because the
    /// window lost focus or the graphics device came back, opens the same menu without a sound.
    /// </summary>
    public sealed class ForcedPauseSoundTests
    {
        [Fact]
        public void ThePauseButtonTaps()
        {
            List<string> played = PlayedWhile(controller => controller.OnButtonPressed(GameControllerButtonId.Pause), out bool menuOpen);

            Assert.True(menuOpen);
            Assert.Contains(Resources.Snd.Tap, played);
        }

        [Fact]
        public void AForcedPauseOpensTheMenuSilently()
        {
            List<string> played = PlayedWhile(controller => Assert.True(controller.EnsurePaused()), out bool menuOpen);

            Assert.True(menuOpen);
            Assert.DoesNotContain(Resources.Snd.Tap, played);
        }

        private static List<string> PlayedWhile(Action<GameController> act, out bool menuOpen)
        {
            _ = HeadlessGame.Boot();
            GameController controller = HeadlessGame.LoadLevelWithController(pack: 1, level: 4);
            GameScene scene = (GameScene)controller.GetView(0).GetChild(GameView.VIEW_ELEMENT_GAME_SCENE);
            HeadlessGame.StepFrames(scene, 60);

            SoundMgr manager = Application.SharedSoundMgr();
            RecordingAudioBackend backend = new();
            bool originalSoundPreference = Preferences.GetBooleanForKey("SOUND_ON");
            SoundMgr.SetBackend(backend);
            // An effect loaded through an earlier backend would play without this one seeing it.
            manager.FreeSound(Resources.Snd.Tap);
            Preferences.SetBooleanForKey(true, "SOUND_ON");

            try
            {
                act(controller);
                menuOpen = controller.GetView(0).GetChild(GameView.VIEW_ELEMENT_PAUSE_MENU).IsEnabled();
                return backend.Played;
            }
            finally
            {
                manager.StopAllSounds();
                manager.FreeSound(Resources.Snd.Tap);
                SoundMgr.SetBackend(null);
                Preferences.SetBooleanForKey(originalSoundPreference, "SOUND_ON");
            }
        }

        private sealed class RecordingAudioBackend : IAudioBackend
        {
            public List<string> Played { get; } = [];

            public AudioPlaybackState MusicState => AudioPlaybackState.Stopped;

            public ISoundEffect LoadSound(string contentPath)
            {
                return new RecordingSoundEffect(this, Path.GetFileNameWithoutExtension(contentPath));
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
        }

        private sealed class RecordingSoundEffect(RecordingAudioBackend backend, string name) : ISoundEffect
        {
            public ISoundInstance CreateInstance()
            {
                return new RecordingSoundInstance(backend, name);
            }

            public void Dispose()
            {
            }
        }

        private sealed class RecordingSoundInstance(RecordingAudioBackend backend, string name) : ISoundInstance
        {
            public bool IsLooped { get; set; }

            public float Volume { get; set; }

            public AudioPlaybackState State { get; private set; } = AudioPlaybackState.Stopped;

            public void Play()
            {
                backend.Played.Add(name);
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
            }
        }
    }
}
