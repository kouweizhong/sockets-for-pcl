﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.UI.ViewManagement;
using PclSocketException = Sockets.Plugin.Abstractions.SocketException;

namespace Sockets.Plugin
{
    public static class NativeExceptionExtensions
    {
        public static Task WrapNativeSocketExceptionsAsTask(this IAsyncAction task, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<bool>();
            if (cancellationToken != default(CancellationToken))
                cancellationToken.Register(task.Cancel);

            task.Completed = delegate(IAsyncAction info, AsyncStatus status)
            {
                switch (status)
                {
                    case AsyncStatus.Canceled:
                        tcs.SetCanceled();
                        break;
                    case AsyncStatus.Completed:
                        tcs.SetResult(true);
                        break;
                    case AsyncStatus.Error:
                        tcs.SetException(info.ErrorCode);
                        break;
                    case AsyncStatus.Started:
                        break;
                    default:
                        break;
                }
            };
            
            return tcs.Task.ContinueWith(
                t =>
                {
                    if (t.IsCanceled)
                        cancellationToken.ThrowIfCancellationRequested();

                    if (!t.IsFaulted)
                        return t;
                    
                    // only on faulted. 
                    var ex = t.Exception.InnerException;
                    var hResult = ex.HResult;
                    var socketError = SocketError.GetStatus(hResult);

                    throw (socketError == SocketErrorStatus.Unknown)
                        ? ex
                        : new PclSocketException(ex);
                });
        }

        public static Task<T> WrapNativeSocketExceptions<T>(this IAsyncOperation<T> task)
        {
            var tcs = new TaskCompletionSource<T>();

            task.Completed = delegate(IAsyncOperation<T> info, AsyncStatus status)
            {
                switch (status)
                {
                    case AsyncStatus.Canceled:
                        tcs.SetCanceled();
                        break;
                    case AsyncStatus.Completed:
                        tcs.SetResult(info.GetResults());
                        break;
                    case AsyncStatus.Error:
                        tcs.SetException(info.ErrorCode);
                        break;
                    case AsyncStatus.Started:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(status), status, null);
                }
            };

            return tcs.Task.ContinueWith(
                t =>
                {
                    if (!t.IsFaulted)
                        return t.Result;

                    // only on faulted. 
                    var ex = t.Exception.InnerException;
                    var hResult = ex.HResult;
                    var socketError = SocketError.GetStatus(hResult);

                    throw (socketError == SocketErrorStatus.Unknown)
                        ? ex
                        : new PclSocketException(ex);
                });
        }
    }
}