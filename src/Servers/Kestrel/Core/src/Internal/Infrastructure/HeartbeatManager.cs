// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure
{
    internal class HeartbeatManager : IHeartbeatHandler
    {
        private readonly ConnectionManager _connectionManager;
        private readonly Action<KestrelConnection> _walkCallback;

        public HeartbeatManager(ConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
            _walkCallback = WalkCallback;
        }

        public void OnHeartbeat(long now)
        {
            _connectionManager.Walk(_walkCallback);
        }

        private void WalkCallback(KestrelConnection connection)
        {
            connection.TickHeartbeat();
        }
    }
}
