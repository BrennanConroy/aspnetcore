// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure
{
    /// <summary>
    /// Abstracts the system clock to facilitate testing.
    /// </summary>
    internal interface ISystemClock
    {
        /// <summary>
        /// Retrieves ticks for the current UTC system time.
        /// </summary>
        long CurrentTicks { get; }

        /// <summary>
        /// Retrieves the current UTC system time.
        /// This is only safe to use from code called by the <see cref="Heartbeat"/>.
        /// </summary>
        long CurrentTicksUnsynchronized { get; }
    }
}
