using System;
using System.Collections.Generic;
using CutTheRope.iframework.helpers;

namespace CutTheRope.ios
{
    internal static class TimerManager
    {
        public static int Schedule(DelayedDispatcher.DispatchFunc callback, NSObject parameter, float interval)
        {
            ArgumentNullException.ThrowIfNull(callback);

            ArgumentOutOfRangeException.ThrowIfLessThan(interval, 0f);

            EnsureInitialized();

            TimerEntry entry = new(callback, parameter, interval);
            int id = nextTimerId++;
            timers.Add(id, entry);
            return id;
        }

        public static void StopTimer(int timerId)
        {
            if (timerId < 0)
            {
                return;
            }

            EnsureInitialized();
            timers.Remove(timerId);
        }

        public static void RegisterDelayedObjectCall(DelayedDispatcher.DispatchFunc callback, NSObject parameter, double interval)
        {
            ArgumentNullException.ThrowIfNull(callback);

            ArgumentOutOfRangeException.ThrowIfLessThan(interval, 0.0);

            EnsureInitialized();
            delayedDispatcher.CallObjectSelectorParamafterDelay(callback, parameter, interval);
        }

        public static void Update(float delta)
        {
            EnsureInitialized();

            if (timers.Count == 0)
            {
                delayedDispatcher.Update(delta);
                return;
            }

            updateKeys.Clear();
            updateKeys.AddRange(timers.Keys);

            foreach (int key in updateKeys)
            {
                if (!timers.TryGetValue(key, out TimerEntry entry))
                {
                    continue;
                }

                entry.Accumulator += delta;
                if (entry.Accumulator >= entry.Delay)
                {
                    entry.Accumulator -= entry.Delay;
                    entry.Invoke();
                }
            }

            delayedDispatcher.Update(delta);
        }

        private static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            lock (initLock)
            {
                if (initialized)
                {
                    return;
                }

                delayedDispatcher = new DelayedDispatcher();
                initialized = true;
            }
        }

        private static readonly Dictionary<int, TimerEntry> timers = new();
        private static readonly List<int> updateKeys = new();
        private static readonly object initLock = new();
        private static DelayedDispatcher delayedDispatcher = null!;
        private static bool initialized;
        private static int nextTimerId;

        private sealed class TimerEntry
        {
            public TimerEntry(DelayedDispatcher.DispatchFunc callback, NSObject parameter, float delay)
            {
                Callback = callback;
                Parameter = parameter;
                Delay = delay;
            }

            public float Delay { get; }

            public float Accumulator { get; set; }

            public DelayedDispatcher.DispatchFunc Callback { get; }

            public NSObject Parameter { get; }

            public void Invoke()
            {
                Callback(Parameter);
            }
        }
    }
}