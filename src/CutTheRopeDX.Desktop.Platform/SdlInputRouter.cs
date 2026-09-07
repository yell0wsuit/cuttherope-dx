using System;
using System.Collections.Generic;
using System.Numerics;

using CutTheRopeDX.Framework.Core;

using SDL3;
namespace CutTheRopeDX.Desktop.Platform
{
    /// <summary>Dispatches SDL edges once, preserving event order and excluding synthetic duplicate devices.</summary>
    internal sealed class SdlInputRouter
    {
        private readonly Dictionary<int, Vector2> pointers = [];
        private readonly Dictionary<(ulong, ulong), int> fingers = [];
        private readonly HashSet<SDL.Keycode> held = [];
        private readonly HashSet<KeyCode> pressed = [];
        private readonly HashSet<uint> gamepadBack = [];
        private int nextFinger = 1;
        private double wheelRemainder;
        public uint WindowId { get; set; }
        public int WindowWidth { get; set; } = 1;
        public int WindowHeight { get; set; } = 1;
        public bool PrimaryPressed => pointers.ContainsKey(0);
        public Action<TouchLocation> Touch { get; set; } = _ => { };
        public Func<float, float, Vector2> MapPosition { get; set; } = (x, y) => new(x, y);
        public Action<Vector2> MouseMoved { get; set; } = _ => { };
        public Action Back { get; set; } = () => { };
        public Action ToggleFullscreen { get; set; } = () => { };
        public Action<int> Wheel { get; set; } = _ => { };
        public Action<bool> FocusChanged { get; set; } = _ => { };
        public Action Quit { get; set; } = () => { };
        public Action Resized { get; set; } = () => { };
        public void Pointer(int id, TouchLocationState state, float x, float y)
        {
            Vector2 position = MapPosition(x, y);
            if (state == TouchLocationState.Pressed)
            {
                if (!pointers.TryAdd(id, position))
                {
                    return;
                }
            }
            else if (!pointers.ContainsKey(id))
            {
                return;
            }
            else if (state == TouchLocationState.Released)
            {
                _ = pointers.Remove(id);
            }
            else
            {
                pointers[id] = position;
            }

            Touch(new TouchLocation(id, state, position));
        }
        public void HandleEvent(in SDL.Event e)
        {
            switch ((SDL.EventType)e.Type)
            {
                case SDL.EventType.Quit: Quit(); break;
                case SDL.EventType.WindowCloseRequested: if (Matches(e.Window.WindowID)) { Quit(); } break;
                case SDL.EventType.WindowFocusLost:
                case SDL.EventType.WindowMinimized:
                    if (Matches(e.Window.WindowID)) { ClearInput(); FocusChanged(false); }
                    break;
                case SDL.EventType.WindowFocusGained:
                    if (Matches(e.Window.WindowID))
                    {
                        FocusChanged(true);
                    }

                    break;
                case SDL.EventType.WindowPixelSizeChanged:
                case SDL.EventType.WindowResized:
                    if (Matches(e.Window.WindowID))
                    {
                        Resized();
                    }

                    break;
                case SDL.EventType.KeyDown:
                case SDL.EventType.KeyUp:
                    if (Matches(e.Key.WindowID))
                    {
                        Key(e.Key.Key, e.Key.Down, e.Key.Repeat, e.Key.Mod);
                    }

                    break;
                case SDL.EventType.MouseButtonDown:
                case SDL.EventType.MouseButtonUp:
                    if (Matches(e.Button.WindowID) && e.Button.Which != uint.MaxValue && e.Button.Button == 1)
                    {
                        Pointer(0, e.Button.Down ? TouchLocationState.Pressed : TouchLocationState.Released, e.Button.X, e.Button.Y);
                    }

                    break;
                case SDL.EventType.MouseMotion:
                    if (Matches(e.Motion.WindowID) && e.Motion.Which != uint.MaxValue)
                    { Pointer(0, TouchLocationState.Moved, e.Motion.X, e.Motion.Y); MouseMoved(MapPosition(e.Motion.X, e.Motion.Y)); }
                    break;
                case SDL.EventType.MouseWheel:
                    if (Matches(e.Wheel.WindowID) && e.Wheel.Which != uint.MaxValue)
                    {
                        Scroll(e.Wheel.Y, e.Wheel.Direction == SDL.MouseWheelDirection.Flipped);
                    }

                    break;
                case SDL.EventType.FingerDown:
                case SDL.EventType.FingerMotion:
                case SDL.EventType.FingerUp:
                    if (!Matches(e.TFinger.WindowID) || e.TFinger.TouchID == ulong.MaxValue)
                    {
                        break;
                    }

                    (ulong TouchID, ulong FingerID) key = (e.TFinger.TouchID, e.TFinger.FingerID);
                    if ((SDL.EventType)e.Type == SDL.EventType.FingerDown && !fingers.ContainsKey(key))
                    {
                        fingers.Add(key, nextFinger++);
                    }

                    if (fingers.TryGetValue(key, out int id))
                    {
                        TouchLocationState state = (SDL.EventType)e.Type == SDL.EventType.FingerDown ? TouchLocationState.Pressed :
                            (SDL.EventType)e.Type == SDL.EventType.FingerUp ? TouchLocationState.Released : TouchLocationState.Moved;
                        Pointer(id, state, e.TFinger.X * WindowWidth, e.TFinger.Y * WindowHeight);
                        if (state == TouchLocationState.Released)
                        {
                            _ = fingers.Remove(key);
                        }
                    }
                    break;
                case SDL.EventType.GamepadButtonDown:
                    if (e.GButton.Button == (byte)SDL.GamepadButton.Back && gamepadBack.Add(e.GButton.Which))
                    {
                        Back();
                    }

                    break;
                case SDL.EventType.GamepadButtonUp:
                    if (e.GButton.Button == (byte)SDL.GamepadButton.Back)
                    {
                        _ = gamepadBack.Remove(e.GButton.Which);
                    }

                    break;
                case SDL.EventType.First:
                    break;
                case SDL.EventType.Terminating:
                    break;
                case SDL.EventType.LowMemory:
                    break;
                case SDL.EventType.WillEnterBackground:
                    break;
                case SDL.EventType.DidEnterBackground:
                    break;
                case SDL.EventType.WillEnterForeground:
                    break;
                case SDL.EventType.DidEnterForeground:
                    break;
                case SDL.EventType.LocaleChanged:
                    break;
                case SDL.EventType.SystemThemeChanged:
                    break;
                case SDL.EventType.DisplayOrientation:
                    break;
                case SDL.EventType.DisplayAdded:
                    break;
                case SDL.EventType.DisplayRemoved:
                    break;
                case SDL.EventType.DisplayMoved:
                    break;
                case SDL.EventType.DisplayDesktopModeChanged:
                    break;
                case SDL.EventType.DisplayCurrentModeChanged:
                    break;
                case SDL.EventType.DisplayContentScaleChanged:
                    break;
                case SDL.EventType.UsableBoundsChanged:
                    break;
                case SDL.EventType.WindowShown:
                    break;
                case SDL.EventType.WindowHidden:
                    break;
                case SDL.EventType.WindowExposed:
                    break;
                case SDL.EventType.WindowMoved:
                    break;
                case SDL.EventType.WindowMetalViewResized:
                    break;
                case SDL.EventType.WindowMaximized:
                    break;
                case SDL.EventType.WindowRestored:
                    break;
                case SDL.EventType.WindowMouseEnter:
                    break;
                case SDL.EventType.WindowMouseLeave:
                    break;
                case SDL.EventType.WindowHitTest:
                    break;
                case SDL.EventType.WindowICCProfChanged:
                    break;
                case SDL.EventType.WindowDisplayChanged:
                    break;
                case SDL.EventType.WindowDisplayScaleChanged:
                    break;
                case SDL.EventType.WindowSafeAreaChanged:
                    break;
                case SDL.EventType.WindowOccluded:
                    break;
                case SDL.EventType.WindowEnterFullscreen:
                    break;
                case SDL.EventType.WindowLeaveFullscreen:
                    break;
                case SDL.EventType.WindowDestroyed:
                    break;
                case SDL.EventType.WindowHDRStateChanged:
                    break;
                case SDL.EventType.WindowSettingsChanged:
                    break;
                case SDL.EventType.TextEditing:
                    break;
                case SDL.EventType.TextInput:
                    break;
                case SDL.EventType.KeymapChanged:
                    break;
                case SDL.EventType.KeyboardAdded:
                    break;
                case SDL.EventType.KeyboardRemoved:
                    break;
                case SDL.EventType.TextEditingCandidates:
                    break;
                case SDL.EventType.ScreenKeyboardShown:
                    break;
                case SDL.EventType.ScreenKeyboardHidden:
                    break;
                case SDL.EventType.MouseAdded:
                    break;
                case SDL.EventType.MouseRemoved:
                    break;
                case SDL.EventType.JoystickAxisMotion:
                    break;
                case SDL.EventType.JoystickBallMotion:
                    break;
                case SDL.EventType.JoystickHatMotion:
                    break;
                case SDL.EventType.JoystickButtonDown:
                    break;
                case SDL.EventType.JoystickButtonUp:
                    break;
                case SDL.EventType.JoystickAdded:
                    break;
                case SDL.EventType.JoystickRemoved:
                    break;
                case SDL.EventType.JoystickBatteryUpdated:
                    break;
                case SDL.EventType.JoystickUpdateComplete:
                    break;
                case SDL.EventType.GamepadAxisMotion:
                    break;
                case SDL.EventType.GamepadAdded:
                    break;
                case SDL.EventType.GamepadRemoved:
                    break;
                case SDL.EventType.GamepadRemapped:
                    break;
                case SDL.EventType.GamepadTouchpadDown:
                    break;
                case SDL.EventType.GamepadTouchpadMotion:
                    break;
                case SDL.EventType.GamepadTouchpadUp:
                    break;
                case SDL.EventType.GamepadSensorUpdate:
                    break;
                case SDL.EventType.GamepadUpdateComplete:
                    break;
                case SDL.EventType.GamepadSteamHandleUpdated:
                    break;
                case SDL.EventType.FingerCanceled:
                    break;
                case SDL.EventType.PinchBegin:
                    break;
                case SDL.EventType.PinchUpdate:
                    break;
                case SDL.EventType.PinchEnd:
                    break;
                case SDL.EventType.ClipboardUpdate:
                    break;
                case SDL.EventType.DropFile:
                    break;
                case SDL.EventType.DropText:
                    break;
                case SDL.EventType.DropBegin:
                    break;
                case SDL.EventType.DropComplete:
                    break;
                case SDL.EventType.DropPosition:
                    break;
                case SDL.EventType.AudioDeviceAdded:
                    break;
                case SDL.EventType.AudioDeviceRemoved:
                    break;
                case SDL.EventType.AudioDeviceFormatChanged:
                    break;
                case SDL.EventType.SensorUpdate:
                    break;
                case SDL.EventType.PenProximityIn:
                    break;
                case SDL.EventType.PenProximityOut:
                    break;
                case SDL.EventType.PenDown:
                    break;
                case SDL.EventType.PenUp:
                    break;
                case SDL.EventType.PenButtonDown:
                    break;
                case SDL.EventType.PenButtonUp:
                    break;
                case SDL.EventType.PenMotion:
                    break;
                case SDL.EventType.PenAxis:
                    break;
                case SDL.EventType.CameraDeviceAdded:
                    break;
                case SDL.EventType.CameraDeviceRemoved:
                    break;
                case SDL.EventType.CameraDeviceApproved:
                    break;
                case SDL.EventType.CameraDeviceDenied:
                    break;
                case SDL.EventType.RenderTargetsReset:
                    break;
                case SDL.EventType.RenderDeviceReset:
                    break;
                case SDL.EventType.RenderDeviceLost:
                    break;
                case SDL.EventType.Private0:
                    break;
                case SDL.EventType.Private1:
                    break;
                case SDL.EventType.Private2:
                    break;
                case SDL.EventType.Private3:
                    break;
                case SDL.EventType.PollSentinel:
                    break;
                case SDL.EventType.User:
                    break;
                case SDL.EventType.Last:
                    break;
                case SDL.EventType.EnumPadding:
                    break;
                default:
                    break;
            }
        }
        private bool Matches(uint id)
        {
            return WindowId == 0 || WindowId == id;
        }

        public void Key(SDL.Keycode key, bool down, bool repeat, SDL.Keymod modifiers)
        {
            if (!down) { _ = held.Remove(key); return; }
            if (repeat || !held.Add(key))
            {
                return;
            }

            if (TryMapKey(key, out KeyCode mapped))
            {
                _ = pressed.Add(mapped);
            }

            if (key == SDL.Keycode.Escape)
            {
                Back();
            }

            if (key == SDL.Keycode.F11 || (key == SDL.Keycode.Return && (modifiers & SDL.Keymod.Alt) != 0))
            {
                ToggleFullscreen();
            }
        }
        public void ClearInput()
        {
            foreach (KeyValuePair<int, Vector2> pair in pointers)
            {
                Touch(new TouchLocation(pair.Key, TouchLocationState.Released, pair.Value));
            }

            pointers.Clear(); fingers.Clear(); held.Clear(); pressed.Clear(); gamepadBack.Clear(); wheelRemainder = 0;
        }
        public bool IsKeyPressed(KeyCode key)
        {
            return pressed.Remove(key);
        }

        public void EndUpdate()
        {
            pressed.Clear();
        }

        public bool IsKeyDown(KeyCode key)
        {
            foreach (SDL.Keycode value in held)
            {
                if (TryMapKey(value, out KeyCode mapped) && mapped == key)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The keys Core asks about, and nothing else. SDL derives arrow and function keycodes
        /// from scancodes, so unlike the ASCII keys they cannot be translated arithmetically.
        /// </summary>
        private static readonly Dictionary<SDL.Keycode, KeyCode> KeyMap = new()
        {
            [SDL.Keycode.Escape] = KeyCode.Escape,
            [SDL.Keycode.Return] = KeyCode.Enter,
            [SDL.Keycode.KpEnter] = KeyCode.Enter,
            [SDL.Keycode.Space] = KeyCode.Space,
            [SDL.Keycode.Left] = KeyCode.Left,
            [SDL.Keycode.Right] = KeyCode.Right,
            [SDL.Keycode.F5] = KeyCode.F5,
        };

        private static bool TryMapKey(SDL.Keycode key, out KeyCode mapped)
        {
            return KeyMap.TryGetValue(key, out mapped);
        }
        public void Scroll(float amount, bool flipped)
        {
            wheelRemainder += amount * (flipped ? -120 : 120);
            int delta = (int)wheelRemainder; wheelRemainder -= delta;
            if (delta != 0)
            {
                Wheel(delta);
            }
        }
    }
}
