using System;
using System.Collections.Generic;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>Validates a frame before transferring ownership of a native candidate.</summary>
    public static class BackendSelector
    {
        /// <summary>Tries the platform preference order, or only an explicit override.</summary>
        public static GraphicsSelection<T> Select<T>(string platform, GraphicsBackendKind? forced,
            Func<GraphicsBackendKind, CandidateLifetime, T> create, Action<T> validate)
            where T : IDisposable
        {
            return Attempt(PreferenceOrder(platform, forced), create, validate);
        }

        /// <summary>The renderers to try on a platform, best first.</summary>
        /// <param name="platform">Platform moniker.</param>
        /// <param name="forced">An explicit override, which is the only candidate when present.</param>
        public static GraphicsBackendKind[] PreferenceOrder(string platform, GraphicsBackendKind? forced)
        {
            return forced.HasValue ? [forced.Value] : platform switch
            {
                "windows" =>
                [
                    GraphicsBackendKind.Vulkan,
                    GraphicsBackendKind.Angle,
                    GraphicsBackendKind.OpenGL,
                ],
                "linux" => [GraphicsBackendKind.Vulkan, GraphicsBackendKind.OpenGL],
                "macos" => [GraphicsBackendKind.Metal, GraphicsBackendKind.OpenGL],
                _ => throw new PlatformNotSupportedException(platform),
            };
        }

        /// <summary>Builds and validates candidates in the given order, keeping the first that works.</summary>
        /// <param name="order">Renderers to try, best first.</param>
        /// <param name="create">Builds one candidate, registering what it acquires as it goes.</param>
        /// <param name="validate">Draws and presents a frame, throwing if the candidate cannot.</param>
        internal static GraphicsSelection<T> Attempt<T>(IReadOnlyList<GraphicsBackendKind> order,
            Func<GraphicsBackendKind, CandidateLifetime, T> create, Action<T> validate)
            where T : IDisposable
        {
            ArgumentNullException.ThrowIfNull(order);
            ArgumentNullException.ThrowIfNull(create);
            ArgumentNullException.ThrowIfNull(validate);
            List<Exception> failures = [];
            foreach (GraphicsBackendKind kind in order)
            {
                CandidateLifetime lifetime = new();
                try
                {
                    T device = create(kind, lifetime);
                    validate(device);
                    return new GraphicsSelection<T>(kind, device, lifetime, failures.AsReadOnly());
                }
                catch (Exception failure)
                {
                    failures.Add(failure);
                    try
                    {
                        lifetime.Dispose();
                    }
                    catch (Exception cleanupFailure)
                    {
                        failures.Add(cleanupFailure);
                    }
                }
            }
            throw new AggregateException("No desktop renderer completed its validation frame.", failures);
        }
    }

    /// <summary>Owns resources acquired during partial native initialization.</summary>
    public sealed class CandidateLifetime : IDisposable
    {
        private readonly Stack<IDisposable> resources = new();
        private bool disposed;

        /// <summary>Registers a dependency immediately after successful acquisition.</summary>
        public T Own<T>(T resource) where T : IDisposable
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentNullException.ThrowIfNull(resource);
            resources.Push(resource);
            return resource;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            List<Exception> failures = [];
            while (resources.TryPop(out IDisposable resource))
            {
                try
                {
                    resource.Dispose();
                }
                catch (Exception failure)
                {
                    failures.Add(failure);
                }
            }
            if (failures.Count != 0)
            {
                throw new AggregateException("Native candidate cleanup failed.", failures);
            }
        }
    }

    /// <summary>A validated device and every dependency acquired with it.</summary>
    public sealed class GraphicsSelection<T> : IDisposable where T : IDisposable
    {
        private readonly CandidateLifetime lifetime;

        internal GraphicsSelection(GraphicsBackendKind kind, T device, CandidateLifetime lifetime,
            IReadOnlyList<Exception> failures)
        {
            Kind = kind;
            Device = device;
            this.lifetime = lifetime;
            Failures = failures;
        }

        /// <summary>The renderer that completed a validation frame.</summary>
        public GraphicsBackendKind Kind { get; }
        /// <summary>The selected device. Its dependencies belong to this selection.</summary>
        public T Device { get; }
        /// <summary>Failures from earlier candidates, in attempt order.</summary>
        public IReadOnlyList<Exception> Failures { get; }
        /// <inheritdoc />
        public void Dispose()
        {
            lifetime.Dispose();
        }
    }
}
