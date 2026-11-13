// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.IO.Pipelines;
using System.Net.Http;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http3;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.Core.WebTransport;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.WebTransport;

internal sealed class WebTransportStream : ConnectionContext, IStreamDirectionFeature, IStreamIdFeature, IConnectionItemsFeature
{
    private readonly CancellationTokenRegistration _connectionClosedRegistration;
    private readonly bool _canWrite;
    private readonly bool _canRead;
    private readonly DuplexPipe _duplexPipe;
    private readonly IFeatureCollection _features;
    private readonly KestrelTrace _log;
    private readonly WebTransportSession? _session;
    private readonly ConnectionContext _streamContext;
    private readonly long _streamId;
    private readonly object _disposeLock = new();
    private IDictionary<object, object?>? _items;
    private Task? _disposeTask;
    private TaskCompletionSource? _abortCompletion;
    private volatile bool _isClosed;

    public override string ConnectionId { get => _streamId.ToString(NumberFormatInfo.InvariantInfo); set => throw new NotSupportedException(); }

    public override IDuplexPipe Transport { get => _duplexPipe; set => throw new NotSupportedException(); }

    public override IFeatureCollection Features => _features;

    public override IDictionary<object, object?> Items
    {
        get => _items ??= new ConnectionItems();
        set => _items = value;
    }

    public long StreamId => _streamId;

    public bool CanRead => _canRead && !_isClosed;

    public bool CanWrite => _canWrite && !_isClosed;

    internal WebTransportStream(Http3StreamContext context, WebTransportStreamType type)
    {
        _canRead = type != WebTransportStreamType.Output;
        _canWrite = type != WebTransportStreamType.Input;
        _log = context.ServiceContext.Log;
        _session = context.WebTransportSession;
        _streamContext = context.StreamContext;

        var streamIdFeature = context.ConnectionFeatures.GetRequiredFeature<IStreamIdFeature>();
        _streamId = streamIdFeature!.StreamId;

        _features = context.ConnectionFeatures;
        _features.Set<IStreamDirectionFeature>(this);
        _features.Set<IStreamIdFeature>(this);
        _features.Set<IConnectionItemsFeature>(this);

        _duplexPipe = new DuplexPipe(context.Transport.Input, context.Transport.Output);

        // will not trigger if closed only of of the directions of a stream. Stream must be fully
        // ended before this will be called. Then it will be considered an abort
        _connectionClosedRegistration = context.StreamContext.ConnectionClosed.Register(static state =>
        {
            var stream = (WebTransportStream)state!;
            // QuicStreamContext signals ConnectionClosed before its processing task completes.
            ThreadPool.UnsafeQueueUserWorkItem(static stream => _ = stream.DisposeOnConnectionClosedAsync(), stream, preferLocal: false);
        }, this);

        ConnectionClosed = _connectionClosedRegistration.Token;
    }

    public override void Abort(ConnectionAbortedException abortReason)
    {
        TaskCompletionSource abortCompletion;
        lock (_disposeLock)
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;
            abortCompletion = _abortCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        try
        {
            _log.Http3StreamAbort(ConnectionId, Http3ErrorCode.InternalError, abortReason);

            if (_canRead)
            {
                _duplexPipe.Input.Complete(abortReason);
            }

            if (_canWrite)
            {
                _duplexPipe.Output.Complete(abortReason);
            }
        }
        finally
        {
            abortCompletion.TrySetResult();
        }
    }

    public override ValueTask DisposeAsync()
    {
        TaskCompletionSource? disposeCompletion = null;
        Task? abortCompletion = null;
        var completePipes = false;
        Task disposeTask;

        lock (_disposeLock)
        {
            if (_disposeTask is null)
            {
                completePipes = !_isClosed;
                _isClosed = true;
                abortCompletion = _abortCompletion?.Task;
                disposeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposeTask = disposeCompletion.Task;
            }

            disposeTask = _disposeTask;
        }

        if (disposeCompletion is not null)
        {
            _ = DisposeAsyncCore(disposeCompletion, completePipes, abortCompletion);
        }

        return new ValueTask(disposeTask);
    }

    private async Task DisposeAsyncCore(TaskCompletionSource disposeCompletion, bool completePipes, Task? abortCompletion)
    {
        try
        {
            _session?.RemoveStream(_streamId);

            await _connectionClosedRegistration.DisposeAsync();

            if (abortCompletion is not null)
            {
                await abortCompletion;
            }

            if (completePipes)
            {
                if (_canRead)
                {
                    await _duplexPipe.Input.CompleteAsync();
                }

                if (_canWrite)
                {
                    await _duplexPipe.Output.CompleteAsync();
                }
            }

            await _streamContext.DisposeAsync();

            if (_streamContext is IDisposable disposableStream)
            {
                disposableStream.Dispose();
            }

            disposeCompletion.TrySetResult();
        }
        catch (Exception ex)
        {
            disposeCompletion.TrySetException(ex);
        }
    }

    private async Task DisposeOnConnectionClosedAsync()
    {
        try
        {
            await DisposeAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(0, ex, $"Unexpected exception in {nameof(WebTransportStream)}.{nameof(DisposeAsync)}.");
        }
    }
}
