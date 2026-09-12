using System;
using System.Collections.Generic;

using SDL3;

using Xunit;

namespace CutTheRopeDX.Desktop.Platform.Tests
{
    public sealed class GamepadLifecycleTests
    {
        [Fact]
        public void ConnectedAndHotpluggedDevicesAreOpenedOnlyOnce()
        {
            List<uint> opened = [];
            using SdlGamepadService service = new(() => [7, 11], id => { opened.Add(id); return (nint)id; }, _ => { });
            service.OpenConnected();
            service.HandleEvent(DeviceEvent(SDL.EventType.GamepadAdded, 7));
            service.HandleEvent(DeviceEvent(SDL.EventType.GamepadAdded, 13));
            Assert.Equal(new uint[] { 7, 11, 13 }, opened);
        }

        [Fact]
        public void RemovalAndShutdownCloseEachOwnedHandleOnce()
        {
            List<nint> closed = [];
            SdlGamepadService service = new(() => [7, 11], id => (nint)id, closed.Add);
            service.OpenConnected();
            service.HandleEvent(DeviceEvent(SDL.EventType.GamepadRemoved, 7));
            service.HandleEvent(DeviceEvent(SDL.EventType.GamepadRemoved, 7));
            Assert.Equal(new nint[] { 7 }, closed);
            service.Dispose();
            service.Dispose();
            Assert.Equal(new nint[] { 7, 11 }, closed);
        }

        [Fact]
        public void FailedOpenDoesNotOwnNullHandleAndCanRetry()
        {
            List<nint> closed = [];
            int attempts = 0;
            SdlGamepadService service = new(() => [7], _ => ++attempts == 1 ? 0 : 27, closed.Add);
            service.OpenConnected();
            service.HandleEvent(DeviceEvent(SDL.EventType.GamepadAdded, 7));
            service.Dispose();
            Assert.Equal(2, attempts);
            Assert.Equal(new nint[] { 27 }, closed);
        }

        [Fact]
        public void DisposedServiceDoesNotReopenDevices()
        {
            int opened = 0;
            SdlGamepadService service = new(() => [7], _ => { opened++; return 7; }, _ => { });
            service.Dispose();
            _ = Assert.Throws<ObjectDisposedException>(service.OpenConnected);
            _ = Assert.Throws<ObjectDisposedException>(() => service.HandleEvent(DeviceEvent(SDL.EventType.GamepadAdded, 7)));
            Assert.Equal(0, opened);
        }

        private static SDL.Event DeviceEvent(SDL.EventType type, uint id)
        {
            SDL.Event e = default;
            e.Type = (uint)type;
            e.GDevice.Which = id;
            return e;
        }
    }
}
