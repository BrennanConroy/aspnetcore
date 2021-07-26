// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure
{
    internal class Heartbeat : IDisposable, ISystemClock
    {
        public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

        private readonly IHeartbeatHandler[] _callbacks;
        private readonly ISystemClock _systemClock;
        private readonly IDebugger _debugger;
        private readonly IKestrelTrace _trace;
        private readonly TimeSpan _interval;
        private readonly Thread _timerThread;
        private volatile bool _stopped;
        private long _now;
        private long _nowTicks;

        public long CurrentTicks => Volatile.Read(ref _nowTicks);

        public long CurrentTicksUnsynchronized => _now;

        public Heartbeat(IHeartbeatHandler[] callbacks, ISystemClock systemClock, IDebugger debugger, IKestrelTrace trace)
        {
            _callbacks = callbacks;
            _systemClock = systemClock;
            _debugger = debugger;
            _trace = trace;
            _interval = Interval;
            _timerThread = new Thread(state => ((Heartbeat)state!).TimerLoop())
            {
                Name = "Kestrel Timer",
                IsBackground = true
            };
        }

        public void Start()
        {
            OnHeartbeat();
            _timerThread.Start(this);
        }

        internal void OnHeartbeat()
        {
            _now = _systemClock.CurrentTicks;
            Volatile.Write(ref _nowTicks, _now);

            try
            {
                foreach (var callback in _callbacks)
                {
                    callback.OnHeartbeat(_now);
                }

                if (!_debugger.IsAttached)
                {
                    var after = _systemClock.CurrentTicks;

                    var duration = TimeSpan.FromMilliseconds(after - _now);

                    if (duration > _interval)
                    {
                        _trace.HeartbeatSlow(duration, _interval, DateTimeOffset.UtcNow);
                    }
                }
            }
            catch (Exception ex)
            {
                _trace.LogError(0, ex, $"{nameof(Heartbeat)}.{nameof(OnHeartbeat)}");
            }
        }

        private void TimerLoop()
        {
            while (!_stopped)
            {
                Thread.Sleep(_interval);

                OnHeartbeat();
            }
        }

        public void Dispose()
        {
            _stopped = true;
            // Don't block waiting for the thread to exit
        }
    }
}
