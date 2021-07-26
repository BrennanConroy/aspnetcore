// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;

namespace Microsoft.AspNetCore.Server.Kestrel.Microbenchmarks
{
    internal class MockSystemClock : ISystemClock
    {
        public long CurrentTicks { get; }
        public long CurrentTicksUnsynchronized { get; }
    }
}
