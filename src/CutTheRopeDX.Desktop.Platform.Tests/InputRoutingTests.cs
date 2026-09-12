using System.Collections.Generic;
using System.Numerics;

using CutTheRopeDX.Framework.Core;

using SDL3;

using Xunit;
namespace CutTheRopeDX.Desktop.Platform.Tests
{
    public sealed class InputRoutingTests
    {
        [Fact]
        public void DragPreservesEveryEdgeAndIgnoresSyntheticMouse()
        {
            List<TouchLocation> touches = [];
            SdlInputRouter input = new() { Touch = touches.Add, MapPosition = (x, y) => new Vector2(x * 2, y * 2) };
            input.Pointer(1, TouchLocationState.Pressed, 10, 20);
            input.Pointer(1, TouchLocationState.Pressed, 10, 20);
            input.Pointer(1, TouchLocationState.Moved, 15, 25);
            input.Pointer(1, TouchLocationState.Released, 20, 30);
            input.Pointer(1, TouchLocationState.Released, 20, 30);
            SDL.Event e = new() { Button = new() { Type = SDL.EventType.MouseButtonDown, Which = uint.MaxValue, Button = 1, X = 20, Y = 30 } };
            input.HandleEvent(e);
            Assert.Equal(3, touches.Count);
            Assert.Equal(TouchLocationState.Moved, touches[1].State);
            Assert.Equal(new Vector2(40, 60), touches[2].Position);
        }
        [Fact]
        public void FocusLossReleasesPointerAndKeysWithoutReplay()
        {
            List<TouchLocation> touches = []; int back = 0;
            SdlInputRouter input = new() { Touch = touches.Add, Back = () => back++ };
            input.Pointer(1, TouchLocationState.Pressed, 2, 3);
            input.Key(SDL.Keycode.Escape, true, false, SDL.Keymod.None);
            input.Key(SDL.Keycode.Escape, true, true, SDL.Keymod.None);
            input.ClearInput();
            input.Pointer(1, TouchLocationState.Moved, 4, 5);
            Assert.Equal(1, back); Assert.Equal(2, touches.Count);
            Assert.Equal(TouchLocationState.Released, touches[1].State);
            Assert.False(input.IsKeyDown(KeyCode.Escape));
        }
        [Fact]
        public void LosingFocusCancelsAHeldPressInsteadOfClickingUnderIt()
        {
            List<TouchLocation> touches = [];
            SdlInputRouter input = new() { Touch = touches.Add };
            input.Pointer(1, TouchLocationState.Pressed, 40, 50);
            touches.Clear();

            input.ClearInput();

            // Core's Button activates when a release lands inside it, so replaying the release
            // where the pointer was held would press whatever it was held over.
            TouchLocation release = Assert.Single(touches);
            Assert.Equal(TouchLocationState.Released, release.State);
            Assert.NotEqual(new Vector2(40, 50), release.Position);
            Assert.True(
                release.Position.X < -1000 && release.Position.Y < -1000,
                $"cancelled press should release outside every view, was {release.Position}");
        }

        [Fact]
        public void TouchesAreScaledByTheLiveWindowSize()
        {
            List<TouchLocation> touches = [];
            SdlInputRouter input = new()
            {
                Touch = touches.Add,
                WindowSize = () => (800, 600),
            };

            SDL.Event down = default;
            down.Type = (uint)SDL.EventType.FingerDown;
            down.TFinger.TouchID = 1;
            down.TFinger.FingerID = 2;
            down.TFinger.X = 0.5f;
            down.TFinger.Y = 0.25f;
            input.HandleEvent(down);

            Assert.Equal(new Vector2(400, 150), touches[0].Position);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MoviePressStaysHeldUntilEveryFingerIsReleased(bool reverseReleaseOrder)
        {
            SdlInputRouter input = new();
            Assert.False(input.PrimaryPressed);

            input.HandleEvent(Finger(SDL.EventType.FingerDown, 1));
            Assert.True(input.PrimaryPressed);
            input.HandleEvent(Finger(SDL.EventType.FingerDown, 2));
            input.HandleEvent(Finger(SDL.EventType.FingerUp, reverseReleaseOrder ? 2ul : 1ul));
            Assert.True(input.PrimaryPressed);
            input.HandleEvent(Finger(SDL.EventType.FingerUp, reverseReleaseOrder ? 1ul : 2ul));
            Assert.False(input.PrimaryPressed);
        }

        [Fact]
        public void MoviePressIncludesMouseAndTouchUntilBothAreReleased()
        {
            SdlInputRouter input = new();
            input.Pointer(0, TouchLocationState.Pressed, 10, 20);
            input.HandleEvent(Finger(SDL.EventType.FingerDown, 1));
            input.Pointer(0, TouchLocationState.Released, 10, 20);
            Assert.True(input.PrimaryPressed);

            input.HandleEvent(Finger(SDL.EventType.FingerUp, 1));
            Assert.False(input.PrimaryPressed);
        }

        [Fact]
        public void FocusLossClearsMovieTouchPressWithoutReplayingHeldMotion()
        {
            SdlInputRouter input = new();
            input.HandleEvent(Finger(SDL.EventType.FingerDown, 1));
            Assert.True(input.PrimaryPressed);

            SDL.Event focusLost = default;
            focusLost.Type = (uint)SDL.EventType.WindowFocusLost;
            input.HandleEvent(focusLost);
            Assert.False(input.PrimaryPressed);
            input.HandleEvent(Finger(SDL.EventType.FingerMotion, 1));
            input.HandleEvent(Finger(SDL.EventType.FingerUp, 1));
            Assert.False(input.PrimaryPressed);

            input.HandleEvent(Finger(SDL.EventType.FingerDown, 1));
            Assert.True(input.PrimaryPressed);
        }

        private static SDL.Event Finger(SDL.EventType type, ulong id)
        {
            SDL.Event finger = default;
            finger.Type = (uint)type;
            finger.TFinger.TouchID = 1;
            finger.TFinger.FingerID = id;
            finger.TFinger.X = 0.5f;
            finger.TFinger.Y = 0.5f;
            return finger;
        }

        [Theory]
        [InlineData(SDL.Keycode.Left, (int)KeyCode.Left)]
        [InlineData(SDL.Keycode.Right, (int)KeyCode.Right)]
        [InlineData(SDL.Keycode.F5, (int)KeyCode.F5)]
        [InlineData(SDL.Keycode.Space, (int)KeyCode.Space)]
        [InlineData(SDL.Keycode.Return, (int)KeyCode.Enter)]
        [InlineData(SDL.Keycode.Escape, (int)KeyCode.Escape)]
        public void EveryKeyCoreAsksAboutIsReported(SDL.Keycode pressed, int expectedCode)
        {
            KeyCode expected = (KeyCode)expectedCode;
            SdlInputRouter input = new();
            input.Key(pressed, true, false, SDL.Keymod.None);
            Assert.True(input.IsKeyDown(expected));
            Assert.True(input.IsKeyPressed(expected));

            // IsKeyPressed reports an edge, so the same press must not report twice.
            Assert.False(input.IsKeyPressed(expected));
        }

        [Fact]
        public void FullscreenEdgesAndFractionalWheelArePreserved()
        {
            int toggles = 0; List<int> wheel = [];
            SdlInputRouter input = new() { ToggleFullscreen = () => toggles++, Wheel = wheel.Add };
            input.Key(SDL.Keycode.F11, true, false, SDL.Keymod.None);
            input.Key(SDL.Keycode.F11, true, true, SDL.Keymod.None);
            input.Key(SDL.Keycode.F11, false, false, SDL.Keymod.None);
            input.Key(SDL.Keycode.Return, true, false, SDL.Keymod.Alt);
            input.Scroll(0.125f, false); input.Scroll(1, true);
            Assert.Equal(2, toggles); Assert.Equal(new[] { 15, -120 }, wheel);
        }
    }
}
