using System;
using System.Collections.Generic;

using SDL3;

namespace CutTheRopeDX.Desktop.Platform
{
    /// <summary>
    /// Owns the open gamepad handles. SDL only delivers gamepad button events for devices that
    /// have been opened, so handling the events is not enough on its own.
    /// </summary>
    /// <param name="enumerate">Instance ids of the devices currently attached.</param>
    /// <param name="open">Opens one device, returning zero when it cannot be opened.</param>
    /// <param name="close">Closes a handle this service owns.</param>
    internal sealed class SdlGamepadService(
        Func<uint[]> enumerate,
        Func<uint, nint> open,
        Action<nint> close) : IDisposable
    {
        private readonly Dictionary<uint, nint> handles = [];
        private bool disposed;

        /// <summary>Opens every device attached before the host started listening for events.</summary>
        public void OpenConnected()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            foreach (uint id in enumerate() ?? [])
            {
                Open(id);
            }
        }

        /// <summary>Tracks devices arriving and leaving while the game runs.</summary>
        /// <param name="e">The SDL event to inspect; anything else is ignored.</param>
        public void HandleEvent(in SDL.Event e)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            // Deliberately not a switch: the populate-switch fixer rewrites one over this enum
            // into every one of its members.
            SDL.EventType type = (SDL.EventType)e.Type;
            if (type == SDL.EventType.GamepadAdded)
            {
                Open(e.GDevice.Which);
            }
            else if (type == SDL.EventType.GamepadRemoved && handles.Remove(e.GDevice.Which, out nint removed))
            {
                close(removed);
            }
        }

        private void Open(uint id)
        {
            if (handles.ContainsKey(id))
            {
                return;
            }

            // A device that fails to open is not owned, so a later add event may retry it.
            nint handle = open(id);
            if (handle != 0)
            {
                handles.Add(id, handle);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (nint handle in handles.Values)
            {
                close(handle);
            }

            handles.Clear();
        }
    }
}
