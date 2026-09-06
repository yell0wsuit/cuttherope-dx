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
