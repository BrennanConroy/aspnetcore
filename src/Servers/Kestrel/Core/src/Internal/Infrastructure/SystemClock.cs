// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure
{
    /// <summary>
    /// Provides access to the normal system clock.
    /// </summary>
    internal class SystemClock : ISystemClock
    {
        /// <summary>
        /// Retrieves ticks for the current system up-time.
        /// </summary>
        public long CurrentTicks => Environment.TickCount64;

        /// <summary>
        /// Retrieves the current system up-time.
        /// </summary>
        public long CurrentTicksUnsynchronized => Environment.TickCount64;
    }
}
